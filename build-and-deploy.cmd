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
rmdir /s /q deploy
mkdir deploy

powershell -Command "(Get-Content src\EcorRouge.Archive.Utility\EcorRouge.Archive.Utility.csproj -Raw) -replace '%OLD_VERSION%','%NEW_VERSION%' | Out-File -encoding UTF8 src\EcorRouge.Archive.Utility\EcorRouge.Archive.Utility.csproj"

dotnet publish -c Release src\EcorRouge.Archive.Utility.sln

IF %SIGNING_MODE%==sign (
  move /y build\net5.0-windows\publish\EcorRouge.Archive.Utility.exe build\net5.0-windows\publish\EcorRouge.Archive.Utility-unsigned.exe
  powershell -file signpath.ps1 build\net5.0-windows\publish\EcorRouge.Archive.Utility-unsigned.exe build\net5.0-windows\publish\EcorRouge.Archive.Utility.exe
  del /s build\net5.0-windows\publish\EcorRouge.Archive.Utility-unsigned.exe
)

rmdir /s /q build\net5.0-windows\publish\plugins
mkdir build\net5.0-windows\publish\plugins\EcorRouge.Archive.Utility.Plugins.S3
mkdir build\net5.0-windows\publish\plugins\EcorRouge.Archive.Utility.Plugins.Local
mkdir build\net5.0-windows\publish\plugins\EcorRouge.Archive.Utility.Plugins.Wasabi

xcopy /y build\plugins\EcorRouge.Archive.Utility.Plugins.S3\net5.0\publish\* build\net5.0-windows\publish\plugins\EcorRouge.Archive.Utility.Plugins.S3\
xcopy /y build\plugins\EcorRouge.Archive.Utility.Plugins.Local\net5.0\publish\* build\net5.0-windows\publish\plugins\EcorRouge.Archive.Utility.Plugins.Local\
xcopy /y build\plugins\EcorRouge.Archive.Utility.Plugins.Wasabi\net5.0\publish\* build\net5.0-windows\publish\plugins\EcorRouge.Archive.Utility.Plugins.Wasabi\

pushd build\net5.0-windows\publish
%ZIP_PATH% a -r ..\..\..\deploy\archive-utility-%NEW_VERSION%.zip *.*
popd

%OPENSSL_PATH% dgst -md5 -binary deploy/archive-utility-%NEW_VERSION%.zip | %OPENSSL_PATH% enc -base64 > deploy/archive-utility-%NEW_VERSION%.md5

%ISS_PATH% installer\utility-1.0-installer.iss

copy /y installer\Output\setup-archive-utility-%NEW_VERSION%-x86.exe deploy\

IF %SIGNING_MODE%==sign (
  move /y deploy\setup-archive-utility-%NEW_VERSION%-x86.exe deploy\setup-archive-utility-%NEW_VERSION%-x86-unsigned.exe
  powershell -file signpath.ps1 deploy\setup-archive-utility-%NEW_VERSION%-x86-unsigned.exe deploy\setup-archive-utility-%NEW_VERSION%-x86.exe
  del /s deploy\setup-archive-utility-%NEW_VERSION%-x86-unsigned.exe
)

@endlocal