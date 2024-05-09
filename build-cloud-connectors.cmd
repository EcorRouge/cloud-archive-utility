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

  copy /Y %ROOT_DIR%cloud_connectors_package_ids.txt cloud_connectors_package_ids.txt

  :: Create solution to contain & publish cloud connectors.
  dotnet new sln --name Connectors.Cloud

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

  dotnet build Connectors.Cloud.sln -c %configuration% --no-self-contained
 
popd

goto :EOF

:GetLastToken
    set "z=%~1"
    if not "%z:*.=%"=="%~1" (
    call :GetLastToken "%z:*.=%")
    goto :EOF

@endlocal