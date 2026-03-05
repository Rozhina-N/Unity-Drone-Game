@echo off
setlocal

rem Ensure we run from the project root (this script's folder)
pushd "%~dp0" >nul 2>&1

rem CRITICAL: Set this FIRST before any Python imports
set "PYTHONDONTWRITEBYTECODE=1"

rem Force clear all __pycache__ directories to ensure fresh code load
echo Clearing Python bytecode cache...
if exist "%~dp0__pycache__" rmdir /s /q "%~dp0__pycache__" 2>nul
if exist "%~dp0drones\__pycache__" rmdir /s /q "%~dp0drones\__pycache__" 2>nul
if exist "%~dp0ui\__pycache__" rmdir /s /q "%~dp0ui\__pycache__" 2>nul
if exist "%~dp0utils\__pycache__" rmdir /s /q "%~dp0utils\__pycache__" 2>nul
if exist "%~dp0websocket\__pycache__" rmdir /s /q "%~dp0websocket\__pycache__" 2>nul
if exist "%~dp0tests\__pycache__" rmdir /s /q "%~dp0tests\__pycache__" 2>nul
if exist "%~dp0logging_config\__pycache__" rmdir /s /q "%~dp0logging_config\__pycache__" 2>nul

set "VENV_PY=%~dp0.venv\Scripts\python.exe"
set "VENV_PYW=%~dp0.venv\Scripts\pythonw.exe"

rem Check for 'venv' alternative if .venv is missing
if not exist "%VENV_PY%" (
    if exist "%~dp0venv\Scripts\python.exe" (
        set "VENV_PY=%~dp0venv\Scripts\python.exe"
    )
)

rem Check if the found venv is actually valid (base python might be missing)
set "VENV_VALID=0"
if exist "%VENV_PY%" (
    "%VENV_PY%" --version >nul 2>&1
    if not errorlevel 1 set "VENV_VALID=1"
)

rem If not found or broken, (re)create it
if "%VENV_VALID%"=="0" (
    echo Virtual environment not found or broken. Creating .venv...
    
    rem Force cleanup of potential broken .venv if it exists
    if exist "%~dp0.venv" rmdir /s /q "%~dp0.venv"
    
    rem Reset path to .venv default
    set "VENV_PY=%~dp0.venv\Scripts\python.exe"
    
    python -m venv "%~dp0.venv"
    if not exist "%VENV_PY%" (
        py -m venv "%~dp0.venv"
    )
    if exist "%VENV_PY%" (
        echo Installing requirements...
        if exist requirements.txt "%VENV_PY%" -m pip install -r requirements.txt
    )
)

rem Prefer pythonw.exe to avoid CLI window
if exist "%VENV_PYW%" (
    start "" /b "%VENV_PYW%" -B __main__.py
    exit /b
) else (
    "%VENV_PY%" -B __main__.py
    if %errorlevel% neq 0 pause
)
:END
popd >nul 2>&1
endlocal
