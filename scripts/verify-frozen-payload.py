#!/usr/bin/env python3
"""Refuse to overwrite or package a mismatched installed legacy payload."""

from pathlib import Path
import sys

repo = Path(__file__).resolve().parents[1]
target = Path(sys.argv[1])


def files(root):
    if not root.is_dir():
        return {}
    return {
        path.relative_to(root): path.read_bytes()
        for path in root.rglob("*")
        if path.is_file()
        and path.name != ".DS_Store"
        and not path.name.startswith("._")
        and path.suffix.lower() not in {".pdb", ".mdb"}
    }


for name in ("1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "Sounds", "Textures"):
    installed = target / name
    if installed.exists() and files(repo / name) != files(installed):
        raise SystemExit(f"Installed frozen payload differs: {installed}")
