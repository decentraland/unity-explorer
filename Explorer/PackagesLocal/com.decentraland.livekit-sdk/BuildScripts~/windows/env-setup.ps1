# env-setup.ps1 - one-time toolchain install for building livekit-ffi on Windows (x86_64).
# Installs: Python 3, VS2022 Build Tools (MSVC v143) + Windows 11 SDK, Git, Rust (MSVC), libclang, protoc, dasel.
# Safe to re-run; each installer skips what is already present.
# This is the bootstrap step: it stays in PowerShell so it runs on a bare machine, and it installs
# the Python that build-win.py then runs on.

[CmdletBinding()]
param(
    [string] $ProtocDir     = "$env:LOCALAPPDATA\protoc",  # where protoc is unpacked
    [string] $DaselDir      = "$env:LOCALAPPDATA\dasel",   # where dasel.exe is placed
    [string] $ProtocVersion = '35.1',                      # pinned, not "latest" (reproducible builds)
    [string] $DaselVersion  = 'v3.11.1'                    # pinned; build-win.py targets this major (v3)
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'            # no slow IWR progress bars
function Step($m) { Write-Host "`n==> $m" -ForegroundColor Cyan }

# 0. Python: build-win.py runs on it, and the libclang step below installs via pip. Install if missing.
Step "Python 3"
$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) {
    winget install --id Python.Python.3.12 --source winget --accept-package-agreements --accept-source-agreements
    # winget does not refresh this session's PATH, so find the python it just installed.
    $python = (Get-Command python -ErrorAction SilentlyContinue).Source
    if (-not $python) {
        $found = Get-ChildItem "$env:LOCALAPPDATA\Programs\Python\Python3*\python.exe" -ErrorAction SilentlyContinue |
                 Sort-Object FullName | Select-Object -Last 1
        if ($found) { $python = $found.FullName }
    }
    if (-not $python) { throw "Python installed but not yet on PATH. Open a new terminal and re-run .\env-setup.ps1." }
}
Write-Host "    python = $python"

# 1. VS2022 Build Tools: C++ toolset (v143) + Windows 11 SDK.
#    Required: the prebuilt webrtc.lib uses VS2022 STL symbols and the Win11 SDK (NTDDI_WIN11_*).
Step "Visual Studio 2022 Build Tools (MSVC v143 + Windows 11 SDK)"
winget install --id Microsoft.VisualStudio.2022.BuildTools --source winget `
  --accept-package-agreements --accept-source-agreements `
  --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.Windows11SDK.26100"

# 2. Git: build-win.py clones the rust-sdks source (with nested submodules) to build from.
Step "Git"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    winget install --id Git.Git --source winget --accept-package-agreements --accept-source-agreements
} else {
    Write-Host "    git already present"
}

# 3. Rust stable, MSVC host (provides cargo/rustc).
Step "Rust (stable, x86_64-pc-windows-msvc)"
if (Get-Command rustup -ErrorAction SilentlyContinue) {
    rustup default stable-x86_64-pc-windows-msvc
} else {
    $init = "$env:TEMP\rustup-init.exe"
    Invoke-WebRequest https://win.rustup.rs/x86_64 -OutFile $init
    & $init -y --default-host x86_64-pc-windows-msvc --default-toolchain stable --profile default
}

# 4. libclang: required by yuv-sys' bindgen. The PyPI package bundles libclang.dll (no admin).
Step "libclang (via pip)"
& $python -m pip install --upgrade libclang
$libclang = & $python -c "import clang, os; print(os.path.join(os.path.dirname(clang.__file__), 'native'))"
Write-Host "    LIBCLANG_PATH = $libclang"

# 5. protoc: required by the livekit-ffi build script (prost-build). Pinned, not "latest", so a
#    future protoc release can't change the toolchain under us.
Step "protoc (v$ProtocVersion)"
$zip = "$env:TEMP\protoc-$ProtocVersion-win64.zip"
Invoke-WebRequest "https://github.com/protocolbuffers/protobuf/releases/download/v$ProtocVersion/protoc-$ProtocVersion-win64.zip" -OutFile $zip
New-Item -ItemType Directory -Force -Path $ProtocDir | Out-Null
tar -xf $zip -C $ProtocDir                              # -> $ProtocDir\bin\protoc.exe
Write-Host "    PROTOC = $ProtocDir\bin\protoc.exe (v$ProtocVersion)"

# 6. dasel: build-win.py uses it to patch [profile.release] in Cargo.toml. Single static binary.
#    Pinned: "latest" drifts across majors (the v2 -> v3 selector-syntax change breaks build-win.py).
Step "dasel ($DaselVersion)"
New-Item -ItemType Directory -Force -Path $DaselDir | Out-Null
Invoke-WebRequest "https://github.com/TomWright/dasel/releases/download/$DaselVersion/dasel_windows_amd64.exe" -OutFile (Join-Path $DaselDir 'dasel.exe')
Write-Host "    DASEL = $DaselDir\dasel.exe ($DaselVersion)"

Step "Toolchain installed. Next: python build-win.py"
