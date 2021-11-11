pushd %~dp0\src

rmdir /s /q build\net5.0-windows\plugins
mkdir build\net5.0-windows\plugins\Reveles.Archive.Utility.Plugins.S3

dotnet publish -c Debug

xcopy /y ..\build\plugins\Reveles.Archive.Utility.Plugins.S3\net5.0\publish\* ..\build\net5.0-windows\plugins\Reveles.Archive.Utility.Plugins.S3\

popd
