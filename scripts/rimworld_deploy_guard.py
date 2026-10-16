#!/usr/bin/env python3
"""Validate and serialize a paired RimWorld mod deployment."""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import os
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Iterator


SAFE_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9]*$")
SAFE_PROPERTY = re.compile(r"^[A-Za-z0-9_.-]+$")
# Matched against the executable name only. Command lines routinely contain
# RimWorld install paths (including this guard's own arguments).
RIMWORLD_EXECUTABLE = re.compile(
    r"RimWorld(?:Win64|Linux)?(?:\.exe)?|RimWorld by Ludeon Studios",
    re.IGNORECASE,
)
TARGET_FRAMEWORK = re.compile(r"<TargetFramework>\s*([^<\s]+)\s*</TargetFramework>")
LEGACY_FRAMEWORK = re.compile(r"net4\d+", re.IGNORECASE)


class DeployError(RuntimeError):
    pass


@dataclass(frozen=True)
class DeploymentPaths:
    logical_mods: Path
    physical_mods: Path
    main_destination: Path
    bridge_root: Path
    bridge_destination: Path


def logical_absolute(path: Path) -> Path:
    return Path(os.path.abspath(os.path.expanduser(str(path))))


def validate_project(path: Path) -> Path:
    logical = logical_absolute(path)
    if logical.suffix.casefold() != ".csproj" or not logical.is_file():
        raise DeployError(f"Main project must be an existing .csproj file: {logical}")
    return logical


def validate_name(value: str, label: str) -> None:
    if not SAFE_NAME.fullmatch(value):
        raise DeployError(f"Refusing unsafe {label}: {value}")


def validate_mod_directory(path: Path, mod_name: str, bridge_name: str) -> DeploymentPaths:
    validate_name(mod_name, "mod name")
    validate_name(bridge_name, "BridgeTools name")
    logical = logical_absolute(path)
    if not logical.is_dir():
        raise DeployError(f"RimWorld Mods directory does not exist: {logical}")
    physical = logical.resolve(strict=True)
    home = Path.home().resolve()
    if (
        physical == Path(physical.anchor)
        or physical == home
        or physical.parent == Path(physical.anchor)
    ):
        raise DeployError(f"Refusing unsafe RimWorld Mods directory: {physical}")
    if logical.name.casefold() != "mods" or physical.name.casefold() != "mods":
        raise DeployError(
            "RimWorld deploy root must logically and physically be a directory "
            f"named Mods: {logical} -> {physical}"
        )

    # A common macOS setup exposes the same physical Mods directory through both
    # an app bundle and a *-UserData alias. The logical parent controls where the
    # sibling BridgeTools directory lands, so this alias is too ambiguous to use.
    if logical.parent.name.endswith("-UserData"):
        base = logical.parent.name[: -len("-UserData")]
        app_mods = logical.parent.parent / f"{base}.app" / "Mods"
        if app_mods.is_dir() and app_mods.resolve(strict=True) == physical:
            raise DeployError(
                "Ambiguous macOS Mods alias: use the canonical .app/Mods path "
                f"instead of {logical}"
            )

    main_destination = logical / mod_name
    bridge_root = logical.parent / "BridgeTools"
    bridge_destination = bridge_root / bridge_name
    for destination, label in (
        (main_destination, "mod destination"),
        (bridge_destination, "BridgeTools destination"),
    ):
        if destination.is_symlink():
            raise DeployError(f"Refusing symlinked {label}: {destination}")
        if destination.exists() and not destination.is_dir():
            raise DeployError(f"{label.title()} is not a directory: {destination}")
    if bridge_root.is_symlink():
        raise DeployError(f"Refusing symlinked BridgeTools root: {bridge_root}")
    if bridge_root.exists() and not bridge_root.is_dir():
        raise DeployError(f"BridgeTools root is not a directory: {bridge_root}")

    return DeploymentPaths(
        logical_mods=logical,
        physical_mods=physical,
        main_destination=main_destination,
        bridge_root=bridge_root,
        bridge_destination=bridge_destination,
    )


def validate_runtime_root(paths: DeploymentPaths, project: Path) -> None:
    """Pick the Mods path whose sibling BridgeTools the target game reads.

    On macOS, RimWorld 1.7 (.NET) runs from <install>/RimWorld.app and reads
    <install>/Mods and <install>/BridgeTools, while the Unity build (1.6 and
    older) reads RimWorldMac.app/Mods and RimWorldMac.app/BridgeTools. Both Mods
    paths may resolve to one physical directory, so only the project's target
    framework decides which logical path deploys the companion correctly.
    """
    match = TARGET_FRAMEWORK.search(project.read_text(errors="replace"))
    if not match:
        return
    framework = match.group(1)
    legacy = bool(LEGACY_FRAMEWORK.fullmatch(framework))
    logical = paths.logical_mods
    if logical.parent.suffix.casefold() == ".app":
        outer = logical.parent.parent / "Mods"
        if (
            not legacy
            and outer.is_dir()
            and outer.resolve(strict=True) == paths.physical_mods
        ):
            raise DeployError(
                f"{framework} mods run in the .NET RimWorld build, which reads "
                f"mods and BridgeTools from {outer.parent}; set RIMWORLD_MOD_DIR={outer}"
            )
        return
    if legacy:
        for app in sorted(logical.parent.glob("*.app")):
            app_mods = app / "Mods"
            if app_mods.is_dir() and app_mods.resolve(strict=True) == paths.physical_mods:
                raise DeployError(
                    f"{framework} mods run in the Unity RimWorld build, which reads "
                    f"BridgeTools from {app}; set RIMWORLD_MOD_DIR={app_mods}"
                )


def lock_path(paths: DeploymentPaths, mod_name: str) -> Path:
    digest = hashlib.sha256(
        f"{paths.physical_mods}\n{mod_name}\n".encode()
    ).hexdigest()
    return Path(tempfile.gettempdir()) / f"steam-mods-deploy-{digest}.lock"


def try_lock(handle: BinaryIO) -> None:
    if os.name == "nt":
        import msvcrt

        handle.seek(0, os.SEEK_END)
        if handle.tell() == 0:
            handle.write(b"\0")
            handle.flush()
        handle.seek(0)
        msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
    else:
        import fcntl

        fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)


def unlock(handle: BinaryIO) -> None:
    if os.name == "nt":
        import msvcrt

        handle.seek(0)
        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
    else:
        import fcntl

        fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


@contextlib.contextmanager
def deployment_lock(paths: DeploymentPaths, mod_name: str) -> Iterator[Path]:
    path = lock_path(paths, mod_name)
    with path.open("a+b") as handle:
        try:
            try_lock(handle)
        except (BlockingIOError, OSError) as exc:
            raise DeployError(
                f"Another deploy already holds the global lock {path}"
            ) from exc
        try:
            yield path
        finally:
            unlock(handle)


def executable_name(line: str) -> str:
    if os.name == "nt":
        # tasklist CSV: "Image Name","PID",...
        return line.split('","', 1)[0].strip('"')
    parts = line.strip().split(None, 1)
    return Path(parts[1]).name if len(parts) == 2 else ""


def running_rimworld() -> str:
    command = (
        ["tasklist", "/FO", "CSV", "/NH"]
        if os.name == "nt"
        else ["ps", "-A", "-o", "pid=,comm="]
    )
    result = subprocess.run(command, text=True, capture_output=True)
    if result.returncode != 0:
        raise DeployError(
            "Could not determine whether RimWorld is running: "
            + (result.stderr or result.stdout).strip()
        )
    return "\n".join(
        line
        for line in result.stdout.splitlines()
        if RIMWORLD_EXECUTABLE.fullmatch(executable_name(line))
    )


def ensure_stopped() -> None:
    if running := running_rimworld():
        raise DeployError(
            "RimWorld is running; stop it through the repository-supported "
            f"workflow before deploying:\n{running}"
        )


def run_inner(
    paths: DeploymentPaths,
    project: Path,
    configuration: str,
    platform: str,
    mod_name: str,
    build_bridge_tools: str,
) -> None:
    for value, label in ((configuration, "configuration"), (platform, "platform")):
        if not SAFE_PROPERTY.fullmatch(value):
            raise DeployError(f"Refusing unsafe {label}: {value}")
    if build_bridge_tools.casefold() not in {"true", "false"}:
        raise DeployError(
            f"BuildBridgeTools must be true or false: {build_bridge_tools}"
        )
    command = [
        "dotnet",
        "msbuild",
        str(project),
        "-nologo",
        "-verbosity:quiet",
        "-consoleloggerparameters:ErrorsOnly",
        "/target:_CopyToRimworldUnlocked",
        f"/property:RIMWORLD_MOD_DIR={paths.logical_mods}",
        f"/property:Configuration={configuration}",
        f"/property:Platform={platform}",
        "/property:RimworldDeployGuardInner=true",
        f"/property:ModFileName={mod_name}",
        f"/property:BuildBridgeTools={build_bridge_tools.casefold()}",
    ]
    result = subprocess.run(command)
    if result.returncode != 0:
        raise DeployError(
            f"Guarded deployment failed with exit code {result.returncode}"
        )


def verify_pair(
    paths: DeploymentPaths,
    mod_name: str,
    bridge_name: str,
    build_bridge_tools: str,
) -> None:
    # Mods that keep frozen folders for older RimWorld versions ship one main
    # assembly per version folder; at least one must exist.
    main_dlls = sorted(
        paths.main_destination.glob(f"*/Assemblies/{mod_name}.dll")
    )
    if not main_dlls:
        raise DeployError(
            "Guarded deployment did not produce a main assembly under "
            f"{paths.main_destination}"
        )
    if build_bridge_tools.casefold() == "true":
        bridge_dll = paths.bridge_destination / f"{bridge_name}.BridgeTools.dll"
        if not bridge_dll.is_file():
            raise DeployError(f"Guarded paired deployment is missing {bridge_dll}")


def deploy(args: argparse.Namespace) -> None:
    project = validate_project(args.project)
    paths = validate_mod_directory(args.mods_dir, args.mod_name, args.bridge_name)
    validate_runtime_root(paths, project)
    with deployment_lock(paths, args.mod_name):
        # This check deliberately lives inside the cross-process lock. No second
        # deploy can pass validation and race a running-game check outside it.
        ensure_stopped()
        run_inner(
            paths,
            project,
            args.configuration,
            args.platform,
            args.mod_name,
            args.build_bridge_tools,
        )
        verify_pair(
            paths,
            args.mod_name,
            args.bridge_name,
            args.build_bridge_tools,
        )


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser()
    sub = result.add_subparsers(dest="command", required=True)
    validate = sub.add_parser("validate")
    validate.add_argument("mods_dir", type=Path)
    validate.add_argument("project", type=Path)
    validate.add_argument("mod_name")
    validate.add_argument("bridge_name")

    deploy_parser = sub.add_parser("deploy")
    deploy_parser.add_argument("mods_dir", type=Path)
    deploy_parser.add_argument("project", type=Path)
    deploy_parser.add_argument("configuration")
    deploy_parser.add_argument("platform")
    deploy_parser.add_argument("mod_name")
    deploy_parser.add_argument("bridge_name")
    deploy_parser.add_argument("build_bridge_tools")
    sub.add_parser("check-stopped")
    return result


def main() -> int:
    args = parser().parse_args()
    try:
        if args.command == "validate":
            project = validate_project(args.project)
            paths = validate_mod_directory(args.mods_dir, args.mod_name, args.bridge_name)
            validate_runtime_root(paths, project)
        elif args.command == "check-stopped":
            ensure_stopped()
        else:
            deploy(args)
        return 0
    except (DeployError, OSError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
