#!/usr/bin/env bash
#
# publish-unity.sh — build BreweryEmpire.Core and stage it (plus its
# dependencies) into the Unity Plugins folder, so a Unity project consumes a
# compiled netstandard2.1 DLL rather than the source.
#
# Usage:  tools/publish-unity.sh
#
# Requires Unity 2021.2+ (for netstandard2.1 support). The DLLs are build
# output; regenerate with this script, don't hand-edit.

set -euo pipefail

# dotnet is not on PATH by default on this machine.
export PATH="$HOME/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
unset DOTNET_ROOT

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

OUT="unity/Assets/Plugins/BreweryEmpire.Core"
mkdir -p "$OUT"

dotnet publish src/BreweryEmpire.Core/BreweryEmpire.Core.csproj -c Release -o "$OUT"

echo
echo "Published to $OUT:"
ls -1 "$OUT"/*.dll
echo
echo "NOTE: System.Text.Json.dll and its transitive deps are included because"
echo "SaveSystem depends on them. If Unity reports a duplicate/ambiguous"
echo "System.Text.Json or System.Memory reference, see the Unity spike plan"
echo "(checkpoint 1) for the fallback."
