@echo off
rem Starts the same HTTPS profile directly when Visual Studio cannot launch its web server.
rem Close this server before starting the project from Visual Studio.
cd /d "%~dp0"
echo Starting Celeste at https://localhost:7096/
echo Keep this window open while using the website.
echo Press Ctrl+C to stop it before running from Visual Studio.
echo.
dotnet run --project "RROMS\RROMS.csproj" --launch-profile https
if errorlevel 1 (
    echo.
    echo Startup failed. Copy the error above when asking for help.
    pause
)
