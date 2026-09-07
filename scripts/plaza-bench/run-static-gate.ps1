<#
.SYNOPSIS
    Runs the shader-optimization static gate (DCL.Editor.ShaderStaticGate.Run) in Unity batchmode.

.DESCRIPTION
    Compiles the configured shader variants for D3D11 and writes per-variant reports (DXBC size,
    STAT instruction counts, resource bindings) + raw bytecode into -OutputDir. Diff two output
    dirs (baseline vs patched) to prove a shader change is or is not a compiler no-op.

    IMPORTANT: -batchmode only, WITHOUT -nographics — shader variant compilation needs the
    graphics device; -nographics silently breaks it.

.EXAMPLE
    .\run-static-gate.ps1 -OutputDir C:\bench\gate-baseline
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$ConfigPath = (Join-Path $PSScriptRoot 'shader-gate-config.json')
)

# --- machine-specific paths (adjust at the top, nothing below needs edits) ---
$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
$ProjectPath     = 'C:\ue\Explorer'

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $UnityEditorPath)) { throw "Unity editor not found at $UnityEditorPath (edit the variable at the top of this script)." }
if (-not (Test-Path $ProjectPath))     { throw "Project not found at $ProjectPath (edit the variable at the top of this script)." }
if (-not (Test-Path $ConfigPath))      { throw "Gate config not found at $ConfigPath." }

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir  = (Resolve-Path $OutputDir).Path
$ConfigPath = (Resolve-Path $ConfigPath).Path
$LogFile    = Join-Path $OutputDir 'unity-shader-gate.log'

$unityArgs = @(
    '-batchmode',
    '-projectPath', ('"{0}"' -f $ProjectPath),
    '-executeMethod', 'DCL.Editor.ShaderStaticGate.Run',
    '-shaderGateConfig', ('"{0}"' -f $ConfigPath),
    '-shaderGateOutput', ('"{0}"' -f $OutputDir),
    '-logFile', ('"{0}"' -f $LogFile)
)

Write-Host "Launching Unity static shader gate"
Write-Host "  editor : $UnityEditorPath"
Write-Host "  project: $ProjectPath"
Write-Host "  config : $ConfigPath"
Write-Host "  output : $OutputDir"

$proc = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArgs -Wait -PassThru

Write-Host "Unity exited with code $($proc.ExitCode)"
Write-Host "Summary: $(Join-Path $OutputDir 'gate-summary.txt')"
Write-Host "Log    : $LogFile"
exit $proc.ExitCode
