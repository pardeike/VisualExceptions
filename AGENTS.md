# Visual Exceptions

## Build

`scripts/build-quiet.sh` builds the RimWorld 1.7 assembly into `1.7/Assemblies` and the
BridgeTools companion into `artifacts/BridgeTools`, then prints `ok`. Full output:
`.build/build.log`. With `RIMWORLD_MOD_DIR` set, the deploy guard
(`scripts/rimworld_deploy_guard.py`) installs the mod and the companion as one pair.

- For 1.7 on macOS, `RIMWORLD_MOD_DIR` is `<RimWorld install>/Mods`, not
  `RimWorldMac.app/Mods`: the .NET build reads the sibling `<install>/BridgeTools`.
  The guard refuses the wrong path.
- `--no-deploy` builds only. The release candidate passes `-p:BuildBridgeTools=false`.
- Quit RimWorld before deploying; the guard checks.
- The companion restores the published `RimBridgeServer.Sdk` 2.0.0 package from NuGet.org.
- Both projects use `Krafs.Rimworld.Ref` 1.7.5005-unstable and public
  `Lib.Harmony.Ref` 3.0.0-rc.5. `NuGet.Config` selects NuGet.org.

## Layout

- Only `1.7/` is built from `Source/`. The `1.2`–`1.6` folders are frozen release binaries.
- 1.7 runs on .NET 10 (`net10.0`) with Harmony 3. `Krafs.Rimworld.Ref` provides game
  references; `Lib.Harmony.Ref` is reference-only because the Harmony mod ships `0Harmony.dll`.
- The exceptions tab is a regular `MainButtonDef` in `1.7/Defs`, hidden by its worker
  while there is nothing to report. Do not modify the main button list directly.

## Release

Put player-visible changes in `PENDING_RELEASE_NOTES.md`. The release ZIP is flat
(mod files at the archive root), matching earlier releases.

## In-game testing

Companion tools (`Source/BridgeTools/VisualExceptionsTools.cs`):
`visualexceptions/get_state`, `visualexceptions/clear_exceptions` and
`visualexceptions/raise_test_exception`, which throws once inside vanilla's
`GameComponentTick` try/catch during play.

After a paired build/deploy and restart, `visualexceptions/validate_patch_metadata`
tests generated and unowned patches across every Harmony patch category.
It also reproduces the inspector's report path with a real generated prefix.
Set `showInspector=true` to leave its test exception open for a rendering check.

GABS game id `visualexceptions-17` launches RimWorld with an isolated profile in
`.build/UserData` (Harmony, RimBridgeServer, Visual Exceptions, Core).
