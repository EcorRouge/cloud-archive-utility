@echo off

@setlocal enableextensions enabledelayedexpansion

set SIGNING_MODE=%1

if "%1"=="" (
  set SIGNING_MODE=nosign 
)

IF NOT EXIST signpath.ps1 (
  set SIGNING_MODE=nosign
)

echo Code signing mode = %SIGNING_MODE%

set ZIP_PATH="C:\Program Files\7-Zip\7z.exe"
set ISS_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set OPENSSL_PATH="C:\OpenSSL-Win64\bin\openssl.exe"
set DEPLOY_DIR=%~dp0\deploy\

set /p SIGNTOOL_ARGS=<signtool_args
echo Signtool arguments: %SIGNTOOL_ARGS%

set /p NEW_VERSION=<version

IF NOT EXIST %ZIP_PATH% (
  echo 7-Zip not found at %ZIP_PATH% !
  exit 1
)

IF NOT EXIST %ISS_PATH% (
  echo InnoSetup not found at %ISS_PATH% !
  exit 1
)

IF NOT EXIST %OPENSSL_PATH% (
  echo OpenSSL not found at %OPENSSL_PATH% !
  exit 1
)

for /f "tokens=*" %%a in ( 
  'powershell -NoP -C "if([string](Get-Content src\EcorRouge.Archive.Utility\EcorRouge.Archive.Utility.csproj) -match '<Version>(\d+.\d+.\d+.\d+)</Version>'){$Matches[1]}"'
) do ( 
  set OLD_VERSION=%%a 
) 
for /l %%a in (1,1,100) do if "%OLD_VERSION:~-1%"==" " set OLD_VERSION=%OLD_VERSION:~0,-1%

echo Previous version=%OLD_VERSION%

if "%NEW_VERSION%" == "" (
  for /f "tokens=1-5* delims=." %%a in ("%OLD_VERSION%") do (
    set /a build=%%d+1
    set NEW_VERSION=%%a.%%b.%%c
  )
  set NEW_VERSION=%NEW_VERSION%.%build%
)

echo New version=%NEW_VERSION%.

rmdir /s /q build
rmdir /s /q "%DEPLOY_DIR%"
mkdir "%DEPLOY_DIR%"

powershell -Command "(Get-Content src\EcorRouge.Archive.Utility\EcorRouge.Archive.Utility.csproj -Raw) -replace '%OLD_VERSION%','%NEW_VERSION%' | Out-File -encoding UTF8 src\EcorRouge.Archive.Utility\EcorRouge.Archive.Utility.csproj"

dotnet publish -c Release src\EcorRouge.Archive.Utility.sln
xcopy /y /i build\* build\publish

IF %SIGNING_MODE%==sign (
  move /y build\publish\EcorRouge.Archive.Utility.exe build\publish\EcorRouge.Archive.Utility-unsigned.exe
  powershell -file signpath.ps1 build\publish\EcorRouge.Archive.Utility-unsigned.exe build\publish\EcorRouge.Archive.Utility.exe
  del /s build\publish\EcorRouge.Archive.Utility-unsigned.exe
)

rmdir /s /q build\publish\plugins
:: /s - Copy folders and subfolders, /i - If in doubt always assume the destination is a folder e.g. when the destination does not exist.
xcopy /y /s /i build\plugins\* build\publish\plugins

nuget sources list | findstr /c:"EcorRouge Feed"
if errorlevel 1 (
    echo Skipping cloud connectors build - no source.
) else (
    echo Building cloud connectors...
    call build-cloud-connectors.cmd "Release"
    xcopy /y /s /i build\connectors\* build\publish\connectors
)

pushd build\publish
  %ZIP_PATH% a -r "%DEPLOY_DIR%archive-utility-%NEW_VERSION%.zip" *.*
popd

%OPENSSL_PATH% dgst -md5 -binary "%DEPLOY_DIR%archive-utility-%NEW_VERSION%.zip" | %OPENSSL_PATH% enc -base64 > "%DEPLOY_DIR%archive-utility-%NEW_VERSION%.md5"

pushd installer
  cscript.exe list-utility-files.vbs
  %ISS_PATH% utility-1.0-installer.iss

  copy /y Output\setup-archive-utility-%NEW_VERSION%-x86.exe "%DEPLOY_DIR%"
popd

REM simplify via pushd %DEPLOY_DIR%
IF %SIGNING_MODE%==sign (
  move /y "%DEPLOY_DIR%setup-archive-utility-%NEW_VERSION%-x86.exe" "%DEPLOY_DIR%setup-archive-utility-%NEW_VERSION%-x86-unsigned.exe"
  powershell -file signpath.ps1 "%DEPLOY_DIR%setup-archive-utility-%NEW_VERSION%-x86-unsigned.exe" "%DEPLOY_DIR%setup-archive-utility-%NEW_VERSION%-x86.exe"
  del /s "%DEPLOY_DIR%setup-archive-utility-%NEW_VERSION%-x86-unsigned.exe"
)

@endlocal