# Socket Monitor - PowerShell run script for Windows
# Requires Python 3.9+ and dependencies from requirements.txt

param(
    [int]$Pid,
    [double]$Interval = 0.5,
    [string]$Output = "socket_reports"
)

Write-Host "Socket Monitor - HTTP/2 Multiplexing Investigation Tool" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# Check Python
try {
    $pythonVersion = python --version 2>&1
    Write-Host "Found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Python is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install Python 3.9+ from https://www.python.org/downloads/"
    exit 1
}

# Check/install dependencies
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
try {
    python -c "import psutil" 2>&1 | Out-Null
} catch {
    Write-Host "Installing dependencies..." -ForegroundColor Yellow
    pip install -r "$scriptDir\requirements.txt"
}

# Build arguments
$args = @()
if ($Pid) {
    $args += "-p", $Pid
}
$args += "-i", $Interval
$args += "-o", $Output

# Run
Write-Host ""
python "$scriptDir\socket_monitor.py" @args
