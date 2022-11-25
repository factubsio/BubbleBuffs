# Bubbles Ultimate Buff Bot Lite Extra Simple (Bubbles)

## About
This mod for Pathfinder Wrath of the Righteous adds an in-game option to spellbooks to create buff routines.

To download a pre-built version of this mod, go to Nexus Mod at https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/195


## Development Setup (Windows)
1. [Install Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (any version)
2. To debug with Visual Studio, be sure to include the "Game Development with Unity" workload.
3. Clone this github repository
4. Go to System Properties > Environment Variables and add WrathPathDebug to the Pathfinder WOTR install path (for example: C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure)

## Debugging
Owlcat's wiki is slightly out of date, but still a good resource at https://github.com/spacehamster/OwlcatModdingWiki/wiki/Debugging#debugging-with-visual-studio

Instructions below are updated for Pathfinder Wrath of the Righteous Definitive Edition
1. To debug, download the [Unity Hub](https://unity3d.com/get-unity/download).  
2. Go https://unity3d.com/get-unity/download/archive download and install version 2020.3.33f1 of the editor (this coincides with the Pathfinder WOTR Definitive Edition Unity update).
3. Navigate to [UnityFolder]\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_development_mono\ and copy UnityPlayer.dll and WinPixEventRuntime.dll to [GameDir] and overwrite it (I prefer to save the original UnityPlayer.dll as UnityPlayer.dll.original).
4. Use text editor open [GameDir]\Wrath_Data\boot.config and add:
```
wait-for-managed-debugger=1
player-connection-debug=1
```
5. Run [GameDir]/Wrath.exe, then you will see a dialog with message "You can attach a managed debugger now if you want"
6. Go to Visual Studio 2022, click menu Debug/Attach Unity Debugger A "Select Unity instance" dialog will show you some thing like..
```
Project              Machine              Type    Port       Information
<no name>            Your_PC_Name         Player  56593      PID:xxxx
```
7. Double click on it, then select ok on the debug dialog prompt to begin debugging.

Note: This project includes a post build option that will automatically update the default mod location in the Pathfinder WOTR mod folder.  To prevent automatically overwriting the existing mod, change the post build option.

### Acknowledgments:  

-   @Balkoth for Buffbot which is the direct inspiration for this
-   Discord members who tested super early broken extra-poop versions!
-   Pathfinder Wrath of The Righteous Discord channel members
-	@Vek17 extra special thanks because this mod is pretty much a copy paste of his to get it off the ground :pray:
-   Join our [Discord](https://discord.gg/bQVwsP7cky)


