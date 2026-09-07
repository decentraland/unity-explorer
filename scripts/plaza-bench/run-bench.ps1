<#
.SYNOPSIS
    Launches a BUILT explorer player with --plaza-bench to run the deterministic plaza recording.

.DESCRIPTION
    The player waits for Genesis Plaza (default spawn) to load, freezes the skybox time of day,
    runs the scripted ~75 s camera path sampling per-frame CPU/GPU times, captures the three
    golden backbuffer PNGs (P1/P2/P3), writes frames.csv + results.json into -OutDir and quits
    (exit code 0 on success).

    TODO(operator): merge with the known raw-build launch recipe before real runs:
      - launch via an INTERACTIVE SCHEDULED TASK in the console session — headless/ssh launches
        hang at the feature-flags stage on the Windows build machine;
      - a cached identity must exist for the Windows profile running the player (the bench flow
        never shows/waits on the sign-in screen by itself);
      - verify liveness with a NON-LOOPBACK http probe;
      - `git clean -fdx` + checkout the shared Unity checkout between patched runs;
      - record the resolved unity-shared-dependencies lock hash next to the results.

.EXAMPLE
    .\run-bench.ps1 -PlayerPath C:\builds\baseline\Decentraland.exe -OutDir C:\bench\baseline-run1 -Lockstep
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PlayerPath,

    [Parameter(Mandatory = $true)]
    [string]$OutDir,

    # Fixed 1/60 s game-time steps: identical frame sequence every run (recommended for golden captures).
    [switch]$Lockstep
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PlayerPath)) { throw "Player not found at $PlayerPath." }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

$playerArgs = @(
    '--plaza-bench', ('"{0}"' -f $OutDir),
    '--disable-hud',                                        # keeps dynamic UI out of the golden captures
    # '--no-livekit-mode',                                  # optional: keep other players out of the run
    # '--skip-auth-screen',                                 # optional: depends on the identity setup above
    '-logFile', ('"{0}"' -f (Join-Path $OutDir 'player.log'))
)

if ($Lockstep) { $playerArgs += '--plaza-bench-lockstep' }

Write-Host "Launching plaza benchmark"
Write-Host "  player: $PlayerPath"
Write-Host "  outdir: $OutDir"
Write-Host "  args  : $($playerArgs -join ' ')"

$proc = Start-Process -FilePath $PlayerPath -ArgumentList $playerArgs -Wait -PassThru

Write-Host "Player exited with code $($proc.ExitCode)"
Write-Host "Results: $(Join-Path $OutDir 'results.json')"
exit $proc.ExitCode
