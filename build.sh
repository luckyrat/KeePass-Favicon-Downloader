#!/bin/bash

# Build project (DLL for Debug)
#xbuild /p:Configuration=Release KeePassFaviconDownloader.sln /p:TargetFrameworkVersion="v4.5"
#rm -rf bin/ obj/

# Create temporary directory
KPTempDir="./plugin"
rm -rf "$KPTempDir"
mkdir "$KPTempDir"
# Copy files
cp ./*.cs "$KPTempDir/"
cp ./HtmlAgilityPack.dll "$KPTempDir/"
cp ./KeePassFaviconDownloader.csproj "$KPTempDir/"
cp -r ./Properties "$KPTempDir/"

# Build PLGX
mono /usr/lib/keepass2/KeePass.exe --plgx-create "$KPTempDir"

# Cleanup
rm -rf "$KPTempDir"
mv "$KPTempDir.plgx" "./KeePassFaviconDownloader.plgx"

# Create debian package
PKG_PATH="./package"
VPATH="/usr/lib/keepass2/plugins"
rm -rf $PKG_PATH
mkdir -p $PKG_PATH$VPATH
mv ./KeePassFaviconDownloader.plgx $PKG_PATH$VPATH/
# TODO actual packaging

# Cleanup
#rm -rf $PKG_PATH

