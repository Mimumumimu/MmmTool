@echo off
rem Update this clone, then publish. The *_update_and_publish_*.bat files call this file with a target.
rem Usage: 00_update_and_publish.bat [all^|tool^|batch^|web^|admin]  (no argument = all)
rem Updates this clone (and submodules) to the latest, then publishes the chosen builds.
rem Runs from anywhere: the repository root is the parent of this folder.
rem cmd re-reads a running batch file by offset, and git pull may rewrite this file.
rem So the first run copies itself to %TEMP% and continues from the copy.
if "%~1"=="--copy" goto :run
set "SELF=%TEMP%\mmmtool_update_and_publish.bat"
copy /y "%~f0" "%SELF%" >nul
"%SELF%" --copy "%~dp0.." "%~1"
exit /b %errorlevel%

:run
setlocal
set "TARGET=%~3"
if "%TARGET%"=="" set "TARGET=all"
if /i not "%TARGET%"=="all" if /i not "%TARGET%"=="tool" if /i not "%TARGET%"=="batch" if /i not "%TARGET%"=="web" if /i not "%TARGET%"=="admin" (
    echo Unknown target: %TARGET%
    pause
    exit /b 1
)
pushd "%~2"

echo [git pull]
git pull --ff-only
if errorlevel 1 goto :failed

echo [git submodule update]
git submodule update --init --recursive
if errorlevel 1 goto :failed

echo [dotnet build MmmToolAll]
dotnet build .\MmmToolAll.slnx
if errorlevel 1 goto :failed

if /i "%TARGET%"=="all"   goto :tool
if /i "%TARGET%"=="tool"  goto :tool
goto :after_tool

:tool
echo [dotnet publish MmmTool]
dotnet publish .\src\App\MmmTool\MmmTool.csproj -p:PublishProfile=win-x64
if errorlevel 1 goto :failed
echo   MmmTool   : %CD%\src\App\MmmTool\bin\Release\publish\MmmTool_^<version^>\

:after_tool
if /i "%TARGET%"=="all"   goto :batch
if /i "%TARGET%"=="batch" goto :batch
goto :after_batch

:batch
echo [dotnet publish MmmBatch]
dotnet publish .\src\App\MmmBatch\MmmBatch.csproj -p:PublishProfile=win-x64
if errorlevel 1 goto :failed
echo   MmmBatch  : %CD%\src\App\MmmBatch\bin\Release\publish\MmmBatch\

:after_batch
if /i "%TARGET%"=="all"   goto :web
if /i "%TARGET%"=="web"   goto :web
goto :after_web

:web
echo [dotnet publish MmmToolWeb]
dotnet publish .\src\App\MmmToolWeb\Server\MmmToolWeb.Server.csproj -p:PublishProfile=win-x64
if errorlevel 1 goto :failed
echo   MmmToolWeb: %CD%\src\App\MmmToolWeb\Server\bin\Release\publish\MmmToolWeb\

:after_web
if /i "%TARGET%"=="all"   goto :admin
if /i "%TARGET%"=="admin" goto :admin
goto :done

:admin
echo [dotnet publish MmmToolAdmin]
dotnet publish .\src\App\MmmToolAdmin\MmmToolAdmin.csproj -p:PublishProfile=win-x64
if errorlevel 1 goto :failed
echo   MmmToolAdmin: %CD%\src\App\MmmToolAdmin\bin\Release\publish\MmmToolAdmin\

:done
echo.
echo Done.
popd
pause
exit /b 0

:failed
echo.
echo Failed. See the messages above.
popd
pause
exit /b 1
