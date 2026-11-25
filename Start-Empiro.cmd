@echo off
setlocal
cd /d "%~dp0"
if exist "artifacts\release\win-x64\Empiro.exe" (
  start "" "artifacts\release\win-x64\Empiro.exe" %*
  exit /b
)
if exist "artifacts\Empiro\Empiro.exe" (
  start "" "artifacts\Empiro\Empiro.exe" %*
  exit /b
)
if exist "Empiro.App\bin\Release\net10.0-windows\Empiro.exe" (
  start "" "Empiro.App\bin\Release\net10.0-windows\Empiro.exe" %*
  exit /b
)
if exist "Empiro.App\bin\Debug\net10.0-windows\Empiro.exe" (
  start "" "Empiro.App\bin\Debug\net10.0-windows\Empiro.exe" %*
  exit /b
)
echo Build Empiro first with build.ps1.
pause
