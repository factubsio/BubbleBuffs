#!/bin/bash
set -e

cd "$(dirname "$0")"

echo "Building BubbleBuffs..."
dotnet build

echo "Copying to Mods folder..."
rm -rf "$HOME/Library/Application Support/Steam/SteamApps/common/Pathfinder Second Adventure/Mods/BubbleBuffs"
cp -r "BubbleBuffs/bin/Debug" "$HOME/Library/Application Support/Steam/SteamApps/common/Pathfinder Second Adventure/Mods/BubbleBuffs"

echo "Done!"
