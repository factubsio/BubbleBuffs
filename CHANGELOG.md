## Version 5.2.4
* Fixed Repository.json, Info.json versions to be in line with mod version
* Bumped game version in Info.json to current
* (Bug Fix) Removed LinkedSlot check that was causing Opposing School spells on Wizards to be never castable by BubbleBuff due to them counting as double credit when calculating the "cost" of buffing, but not counting the double slots they occupy when slotting in calculation of available credit for buffing.
* Added ADDB's expanded party slots
* Added Ham's scrolling party slots and scrolling expanded button options

## Version 5.1.4
* Fixes and stuff

## Version 5.1.0
(No idea what happened in the other versions...)
* (Feature) Added ability to control the Azata Zippy Magic Secondary Buff.  This option is enabled if a character chooses the Azata Mythic Path and selects the Zippy Magic superpower.  With the option checked, every second cast of the chosen buff is free.  This allows using this mod in a controlled manner with Zippy Magic.
* (Feature) Implemented enhancements to Powerful Change / Share Transmutation if character is a Level 20 Brown-Fur Transmuter with Transmutation Supremacy
* (Feature) Prevent spending an Arcane Reservoir point when using Share Transmutation on self
* (Bug Fix) Buffs from Opposition Schools did not correctly count the number of characters that could be targeted.
* (Bug Fix) Remove fixed paths to D: in the csproj file
* (Bug Fix) Add missing \ for InputLegacyModule path in csproj file
* (Bug Fix) Add missing assets in Assets\icons folder
* (Bug Fix) Update WrathPath environment variable to WrathPathDebug in csproj file.  No need for two environment variables.
* (Maintenance) Update changelog
* (Maintenance) Increment Version Number
* (Quality of Life) Update Readme.md to be helpful for working with project


## Version 0.2.0

* Added "disband army" button
* Fixed a bug where the global map speed would not change until the map was loaded/unloaded.

## Version 0.1.0

* Added speed controls for combat, global map, and tactical battles
