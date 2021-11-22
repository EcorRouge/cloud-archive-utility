# cloud-archive-utility
A utility to help get files off into long-term cloud storage

## Building

**Prerequisites**

* [Dotnet 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0) - please install and make sure that `dotnet` command is available via command prompt
* [7-Zip](https://www.7-zip.org/) - select 64-bit version and make sure it's installed into default path `C:\Program Files\7-Zip\7z.exe`
* [OpenSSL](https://wiki.openssl.org/index.php/Binaries) Alternative links: [link1](https://slproweb.com/products/Win32OpenSSL.html) [link2](https://indy.fulgan.com/SSL/). Make sure to install 64-bit version here: `C:\OpenSSL-Win64\bin\openssl.exe`. Or, after installing to program files, make a symbolic link to `C:\OpenSSL-Win64`. E.g., `mklink /d C:\OpenSSL-Win64 "C:\Program Files\OpenSSL-1.1.1\x64"`
* [Innosetup 6.x](https://jrsoftware.org/isdl.php) - please install version 6

Run `build-and-deploy.cmd` [build-and-deploy.cmd](build-and-deploy.cmd) and see it's output. Binaries will be stored in `deploy` folder. You can change version by modifying `version` file [version](version)

For debugging, when you run from Visual Studio, please use `build-plugins.cmd` [build-plugins.cmd](build-plugins.cmd) which will build/copy all plugins to the output folder.
