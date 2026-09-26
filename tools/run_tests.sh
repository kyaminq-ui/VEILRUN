#!/usr/bin/env sh
# Builds the C# project and runs the in-engine test suite headless.
# Usage: tools/run_tests.sh [filter]     Godot path: $GODOT
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GODOT="${GODOT:-$HOME/Desktop/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
dotnet build "$ROOT/VEILRUN.csproj" -nologo -v q
if [ -n "$1" ]; then
  "$GODOT" --headless --path "$ROOT" res://Scenes/Tests/TestRunner.tscn -- "--filter=$1"
else
  "$GODOT" --headless --path "$ROOT" res://Scenes/Tests/TestRunner.tscn
fi
