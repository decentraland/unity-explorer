@echo off
REM Socket Monitor - Run script for Windows
REM Requires Python 3.9+ and dependencies from requirements.txt

echo Checking Python installation...
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python 3.9+ from https://www.python.org/downloads/
    pause
    exit /b 1
)

echo Checking dependencies...
python -c "import psutil" >nul 2>&1
if errorlevel 1 (
    echo Installing dependencies...
    pip install -r "%~dp0requirements.txt"
)

echo.
echo Starting Socket Monitor...
echo.

python "%~dp0socket_monitor.py" %*

pause
