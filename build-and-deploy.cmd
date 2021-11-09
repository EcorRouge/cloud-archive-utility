@echo off

@setlocal enableextensions enabledelayedexpansion

set ZIP_PATH="C:\Program Files\7-Zip\7z.exe"
set ISS_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set OPENSSL_PATH="C:\OpenSSL-Win64\bin\openssl.exe"

for /f "delims=" %%a in ( 
   'WHERE /R "C:\Program Files (x86)\Windows Kits" signtool.exe' 
) do (
    set __CURRENT_FILE=%%a
    if not "!__CURRENT_FILE:x64=!" == "!__CURRENT_FILE!" (
       set SIGNTOOL=%%a
    )
)

echo Using signtool: %SIGNTOOL%
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
  'powershell -NoP -C "if([string](Get-Content src\Reveles.Archive.Utility\Reveles.Archive.Utility.csproj) -match '<Version>(\d+.\d+.\d+.\d+)</Version>'){$Matches[1]}"'
) do ( 
  set OLD_VERSION=%%a 
) 
for /l %%a in (1,1,100) do if "%OLD_VERSION:~-1%"==" " set OLD_VERSION=%OLD_VERSION:~0,-1%

echo Previous version=%OLD_VERSION%.

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

powershell -Command "(Get-Content src\Reveles.Archive.Utility\Reveles.Archive.Utility.csproj -Raw) -replace '%OLD_VERSION%','%NEW_VERSION%' | Out-File -encoding UTF8 src\Reveles.Archive.Utility\Reveles.Archive.Utility.csproj"

dotnet build
dotnet publish

if not "%SIGNTOOL%" == "" (
  echo "%SIGNTOOL%" sign %SIGNTOOL_ARGS% build\net5.0\publish\Reveles.Archive.Utility.exe
  "%SIGNTOOL%" sign %SIGNTOOL_ARGS% build\net5.0\publish\Reveles.Archive.Utility.exe
)

cd build/net5.0
xcopy /s /i /y ..\net5.0-windows\* .
mkdir plugins
xcopy /s /i /y ..\..\plugins\* plugins\
%ZIP_PATH% a -r ..\..\deploy\collector-%NEW_VERSION%.zip *.*
cd ..\..\

%OPENSSL_PATH% dgst -md5 -binary deploy/archive-utility-%NEW_VERSION%.zip | %OPENSSL_PATH% enc -base64 > deploy/archive-utility-%NEW_VERSION%.md5

%ISS_PATH% installer\utility-1.0-installer.iss

copy /y installer\Output\setup-archive-utility-%NEW_VERSION%-x86.exe deploy\

if not "%SIGNTOOL%" == "" (
  echo "%SIGNTOOL%" sign %SIGNTOOL_ARGS% deploy\setup-archive-utility-%NEW_VERSION%-x86.exe
  "%SIGNTOOL%" sign %SIGNTOOL_ARGS% deploy\setup-archive-utility-%NEW_VERSION%-x86.exe
)

@endlocal