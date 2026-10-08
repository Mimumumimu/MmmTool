@echo off
rem Update this clone (and submodules) to the latest, then publish distribution builds of MmmTool and MmmBatch.
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

echo [1/4] git pull
git pull --ff-only
if errorlevel 1 goto :failed

echo [2/4] git submodule update
git submodule update --init --recursive
if errorlevel 1 goto :failed

echo [3/4] dotnet publish MmmTool
dotnet publish .\src\App\MmmTool\MmmTool.csproj -p:PublishProfile=win-x64
if errorlevel 1 goto :failed

rem MmmBatch has no publish profile or distribution layout yet: a plain framework-dependent publish.
echo [4/4] dotnet publish MmmBatch
dotnet publish .\src\App\MmmBatch\MmmBatch.csproj -c Release -r win-x64 --self-contained false -o .\src\App\MmmBatch\bin\Release\publish\MmmBatch
if errorlevel 1 goto :failed

echo.
echo Done.
echo   MmmTool : %CD%\src\App\MmmTool\bin\Release\publish\MmmTool_^<version^>\
echo   MmmBatch: %CD%\src\App\MmmBatch\bin\Release\publish\MmmBatch\
popd
pause
exit /b 0

:failed
echo.
echo Failed. See the messages above.
popd
pause
exit /b 1
