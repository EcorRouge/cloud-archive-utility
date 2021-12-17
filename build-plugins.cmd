pushd %~dp0\src

rmdir /s /q build\net5.0-windows\plugins
mkdir build\net5.0-windows\plugins\EcorRouge.Archive.Utility.Plugins.S3

dotnet publish -c Debug

xcopy /y ..\build\plugins\EcorRouge.Archive.Utility.Plugins.S3\net5.0\publish\* ..\build\net5.0-windows\plugins\EcorRouge.Archive.Utility.Plugins.S3\
xcopy /y ..\build\plugins\EcorRouge.Archive.Utility.Plugins.Local\net5.0\publish\* ..\build\net5.0-windows\plugins\EcorRouge.Archive.Utility.Plugins.Local\

popd
