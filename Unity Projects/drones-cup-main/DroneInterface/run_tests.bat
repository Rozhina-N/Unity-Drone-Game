@echo off
setlocal
pushd "%~dp0" >nul 2>&1
set "VENV_PY=%~dp0.venv\Scripts\python.exe"
if not exist "%VENV_PY%" (
    echo Virtual environment not found. Please run start.bat first.
    exit /b 1
)
"%VENV_PY%" -m unittest discover -v
popd >nul 2>&1
endlocal
