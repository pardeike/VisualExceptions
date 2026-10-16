#!/usr/bin/env bash
# Builds the RimWorld 1.7 assembly and its BridgeTools companion. With
# RIMWORLD_MOD_DIR set (for 1.7: <RimWorld install>/Mods), the deploy guard
# installs the mod and the companion as one pair. Prints "ok" on success.
# Full output: .build/build.log
#   --no-deploy                   build only
#   -p:BuildBridgeTools=false     release candidate: no companion
#   other arguments               passed to dotnet build
set -uo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
log="$root/.build/build.log"
mkdir -p "$root/.build"

companion=true
args=()
for arg in "$@"; do
	case "$arg" in
		--no-deploy) unset RIMWORLD_MOD_DIR ;;
		*BuildBridgeTools=false*) companion=false; args+=("$arg") ;;
		*) args+=("$arg") ;;
	esac
done

if ! dotnet build "$root/Source/VisualExceptions.csproj" -c Release -nologo ${args[@]+"${args[@]}"} >"$log" 2>&1; then
	echo "build failed:" >&2
	errors="$(grep -E ': (error|warning) |^error: ' "$log" | sed "s|$root/||; s| \[.*||" | sort -u | head -20)"
	if [[ -n "$errors" ]]; then echo "$errors" >&2; else tail -15 "$log" >&2; fi
	echo "log: $log" >&2
	exit 1
fi

fail() { echo "verify: $1; log: $log" >&2; exit 1; }
for f in VisualExceptions.dll CrossPromotion.dll; do
	[[ -f "$root/1.7/Assemblies/$f" ]] || fail "1.7/Assemblies/$f missing"
done
for f in 0Harmony.dll RimBridgeServer.Sdk.dll; do
	[[ ! -e "$root/1.7/Assemblies/$f" ]] || fail "1.7/Assemblies must not contain $f"
done
if [[ "$companion" == true ]]; then
	[[ -f "$root/artifacts/BridgeTools/VisualExceptions/VisualExceptions.BridgeTools.dll" ]] || fail "companion was not built"
fi
if [[ -n "${RIMWORLD_MOD_DIR:-}" ]]; then
	# Lexical parent, as MSBuild and the deploy guard use: Mods may be a symlink.
	cmp -s "$root/1.7/Assemblies/VisualExceptions.dll" "$RIMWORLD_MOD_DIR/VisualExceptions/1.7/Assemblies/VisualExceptions.dll" \
		|| fail "deployed mod does not match the build"
	if [[ "$companion" == true ]]; then
		cmp -s "$root/artifacts/BridgeTools/VisualExceptions/VisualExceptions.BridgeTools.dll" \
			"$(dirname "$RIMWORLD_MOD_DIR")/BridgeTools/VisualExceptions/VisualExceptions.BridgeTools.dll" \
			|| fail "deployed companion does not match the build"
	fi
fi
echo ok
