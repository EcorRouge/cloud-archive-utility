@echo off
set /p username="Enter your github user name: "
set /p passwd="Enter your github password: "

dotnet nuget remove source "EcorRouge Feed"
dotnet nuget add source "https://nuget.pkg.github.com/EcorRouge/index.json" -n "EcorRouge Feed" -u %username% -p %passwd%

dotnet restore
