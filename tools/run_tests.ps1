# Builds the C# project and runs the in-engine test suite headless.
# Usage:  ./tools/run_tests.ps1 [-Filter Motor]
# Godot path: $env:GODOT, else the default Desktop install.
param([string]$Filter = "")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = if ($env:GODOT) { $env:GODOT } else { Join-Path $HOME "Desktop\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" }

dotnet build (Join-Path $root "VEILRUN.csproj") -nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

$userArgs = @()
if ($Filter) { $userArgs = @("--", "--filter=$Filter") }
& $godot --headless --path $root "res://Scenes/Tests/TestRunner.tscn" @userArgs
exit $LASTEXITCODE
