@echo off
rem Update this clone (and submodules) to the latest, then publish a distribution build.
rem Runs from anywhere: the repository root is the parent of this folder.
rem cmd re-reads a running batch file by offset, and git pull may rewrite this file.
rem So the first run copies itself to %TEMP% and continues from the copy.
if "%~1"=="--copy" goto :run
set "SELF=%TEMP%\mmmtool_update_and_publish.bat"
copy /y "%~f0" "%SELF%" >nul
"%SELF%" --copy "%~dp0.."
exit /b %errorlevel%

:run
setlocal
pushd "%~2"

echo [1/3] git pull
git pull --ff-only
if errorlevel 1 goto :failed

echo [2/3] git submodule update
git submodule update --init --recursive
if errorlevel 1 goto :failed

echo [3/3] dotnet publish
dotnet publish .\MmmTool\MmmTool.csproj -p:PublishProfile=win-x64
if errorlevel 1 goto :failed

echo.
echo Done. Output: %CD%\MmmTool\bin\Release\publish\MmmTool_^<version^>\
popd
pause
exit /b 0

:failed
echo.
echo Failed. See the messages above.
popd
pause
exit /b 1
