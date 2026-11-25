@echo off
REM Build and run the C# patcher's test suite.
REM
REM Compiles the tool's core (src\patcher_gui\*.cs -- everything except the
REM WinForms layer under ui\) together with the cases here, embedding the same
REM manifest and ape.act template resources the shipped exe embeds. The
REM template is the C++ Release build: build the x86-release preset first.
REM
REM   run.cmd            all cases, in every culture the runner sweeps
REM   run.cmd Session    only cases whose class or name contains "Session"
setlocal
set HERE=%~dp0
set ROOT=%HERE%..\..
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set OUT=%HERE%bin\ApePatcherTests.exe
set APEACT=%ROOT%\build\x86-release\ape.act

if not exist "%APEACT%" (
  echo No %APEACT%: build the x86-release preset first ^(cmake --build --preset x86-release^).
  exit /b 1
)

REM The stale-manifest guard (umbrella ticket 08): the checked-in manifest must
REM still match what tools\re-static\ produces.
python "%ROOT%\tools\re-static\gen_manifest.py" --check
if errorlevel 1 exit /b 1

if not exist "%HERE%bin" mkdir "%HERE%bin"

"%CSC%" /nologo /target:exe /out:"%OUT%" /debug+ /warnaserror+ ^
  /reference:System.dll /reference:System.Drawing.dll ^
  /resource:"%ROOT%\data\manifest\ape-manifest.json",ape-manifest.json ^
  /resource:"%APEACT%",ape.act ^
  "%ROOT%\src\patcher_gui\*.cs" "%HERE%*.cs"
if errorlevel 1 exit /b 1

"%OUT%" %*
exit /b %errorlevel%
