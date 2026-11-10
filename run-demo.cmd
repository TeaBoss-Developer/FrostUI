@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "samples\FrostUI.Demo\FrostUI.Demo.csproj" -- %*
