@echo off
setlocal
cd /d "%~dp0"

set "DESIGNER_DLL=designer\FrostUI.Designer\bin\Release\net9.0-windows\FrostUI.Designer.dll"
if exist "%DESIGNER_DLL%" (
    rem Run through the signed dotnet host. Some HIPS products block the
    rem unsigned apphost from writing outside the workspace.
    dotnet "%DESIGNER_DLL%" %*
    exit /b %errorlevel%
)

dotnet run --project "designer\FrostUI.Designer\FrostUI.Designer.csproj" -- %*
