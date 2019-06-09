#!/bin/sh

# Remove old builds
if [ -d "publish" ]; then 
	rm -rf publish
fi

# Build
dotnet build -r win7-x64 -c Release
dotnet build -r ubuntu.14.04-x64 -c Release

# Publish
dotnet publish -r win7-x64 -c Release -o publish/windows-Release
dotnet publish -r ubuntu.14.04-x64 -c Release -o publish/linux-Release

# Automaticly build gamemodes and add them to the server

mkdir publish/windows-Release/Gamemodes
mkdir publish/linux-Release/Gamemodes

cd ../Freeroam
dotnet build -c Release

dotnet publish --no-dependencies -o ../gtaserver.core/publish/windows-Release/Gamemodes
dotnet publish --no-dependencies -o ../gtaserver.core/publish/linux-Release/Gamemodes

cd ../gtaserver.core

# Zip
if [ "$OSTYPE" == "win32" ] || [ "$OSTYPE" == "msys" ]; then	
	# zip using 7zip on windows
	"7z" a publish/windows-Release.zip publish/windows-Release/ -r
	"7z" a publish/linux-Release.zip publish/linux-Release/ -r
else
	zip -r publish/windows-Release.zip publish/windows-Release
	zip -r publish/linux-Release.zip publish/linux-Release
fi