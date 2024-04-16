@setlocal enableextensions enabledelayedexpansion

SET ROOT_DIR=%~dp0
SET CLOUD_SLN_DIR=%~dp0build\cloud_connectors\

SET configuration=%1

if "%configuration%"=="" (
    set configuration=Debug
)


rmdir /s /q %CLOUD_SLN_DIR%
mkdir %CLOUD_SLN_DIR%
copy /Y CloudConnectorRef.csproj.template %CLOUD_SLN_DIR%CloudConnectorRef.csproj.template

pushd %CLOUD_SLN_DIR%

  if "%configuration%"=="Debug" (
      :: Pull ids of the structured connectors packages from the Nuget package feed
      %ROOT_DIR%nuget.exe search Reveles.Cloud.Connector. -Source "EcorRouge Feed" -Take 100 -NonInteractive -Verbosity quiet | findstr /c:Cloud\.Connector > ids-raw.txt
      del cloud_connectors_package_ids.txt
      for /F "tokens=2,4" %%A in (ids-raw.txt) do echo %%A %%B>> cloud_connectors_package_ids.txt
      del ids-raw.txt
  ) else (
      copy /Y %ROOT_DIR%cloud_connectors_package_ids.txt cloud_connectors_package_ids.txt
  )

  :: Create solution to contain & publish cloud connectors.
  dotnet new sln --name Connectors.Cloud

  :: Reveles.Collector.Cloud.Connector contains abstractions necessary for all specific connectors - use latest
  mkdir Reveles.Collector.Cloud.Connector.Ref
  copy /Y CloudConnectorRef.csproj.template Reveles.Collector.Cloud.Connector.Ref\Reveles.Collector.Cloud.Connector.Ref.csproj
  pushd Reveles.Collector.Cloud.Connector.Ref
    dotnet add package Reveles.Collector.Cloud.Connector  --no-restore
  popd
  dotnet sln add Reveles.Collector.Cloud.Connector.Ref

  :: For each packageId, create empty console project, add it to the solution, add package to the project.
  FOR /F "tokens=1,2" %%S in (cloud_connectors_package_ids.txt) DO (

     mkdir %%~nxS.Ref

     copy /Y CloudConnectorRef.csproj.template %%~nxS.Ref\%%~nxS.Ref.csproj

     pushd %%~nxS.Ref
        dotnet add package %%~nxS  --no-restore --version %%T

        set z=%%S
        call :GetLastToken !z!
        echo using System.Runtime.CompilerServices;[assembly:TypeForwardedTo(typeof(%%~nxS.!z!Connector^)^)]>>TypeForwarders.cs
     popd
     dotnet sln add %%~nxS.Ref
  )

  dotnet build Connectors.Cloud.sln -c Release --no-self-contained
 
popd

goto :EOF

:GetLastToken
    set "z=%~1"
    if not "%z:*.=%"=="%~1" (
    call :GetLastToken "%z:*.=%")
    goto :EOF

@endlocal