@echo off
REM Build APE Patch Extensions -- one exe, for the SH3 game root.
REM
REM   build.cmd            build to dist\ApePatchExtensions.exe
REM   build.cmd run        build, then launch it against the live install
REM
REM Steps, in order, because each one guards the next:
REM   1. regenerate-and-diff the patch manifest (umbrella ticket 08's stale-
REM      manifest guard) -- a drifted manifest fails the build
REM   2. rebuild the application icon from the portrait art
REM   3. compile, embedding the manifest, the art and the icon; binding the icon
REM      to the exe and the DPI/common-controls manifest to its win32 resources.
REM      AssemblyInfo.cs (picked up by the *.cs wildcard below) supplies the
REM      Win32 version resource -- FileDescription/CompanyName/ProductName --
REM      so the exe doesn't ship with blank publisher metadata.
setlocal
set HERE=%~dp0
set ROOT=%HERE%..\..
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set DIST=%ROOT%\dist
set ART=%ROOT%\data\art\blinkingape.png
set ICO=%ROOT%\data\art\blinkingape.ico
set MANIFEST=%ROOT%\data\manifest\ape-manifest.json

REM Version.cs is the single source of truth (ticket 03); pull the number out
REM of its one const line so the exe's name can never drift from the header.
for /f "tokens=2 delims==;" %%v in ('findstr /c:"const string Current" "%HERE%Version.cs"') do set APE_VERSION_RAW=%%v
set APE_VERSION=%APE_VERSION_RAW:"=%
set APE_VERSION=%APE_VERSION: =%
set OUT=%DIST%\ApePatchExtensions_v%APE_VERSION%.exe

python "%ROOT%\tools\re-static\gen_manifest.py" --check
if errorlevel 1 exit /b 1

python "%ROOT%\tools\make_icon.py"
if errorlevel 1 exit /b 1

if not exist "%DIST%" mkdir "%DIST%"

"%CSC%" /nologo /target:winexe /out:"%OUT%" /optimize+ /warnaserror+ ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /resource:"%MANIFEST%",ape-manifest.json ^
  /resource:"%ART%",ape.png ^
  /resource:"%ICO%",ape.ico ^
  /win32icon:"%ICO%" ^
  /win32manifest:"%HERE%app.manifest" ^
  "%HERE%*.cs" "%HERE%ui\*.cs"
if errorlevel 1 exit /b 1

echo built %OUT%

if /i "%~1"=="run" start "" "%OUT%" %2 %3
exit /b 0
