# RimWorld Visual Exceptions

This mod shows you exceptions graphically, for example like this:

<img src="https://i.imgur.com/EeHDKz1.png"/>

# Installation

Choose **Releases** on the right, find the latest release and in the Assets field download `VisualExceptions.zip`. Once downloaded, unzip to get a folder called `VisualExceptions`. Put that folder into your RimWorld `Mods` folder. Follow [this guide](https://rimworldwiki.com/wiki/Installing_mods) on how to install mods offline.

# Usage

Installing the mod for the first time will default to it being active. Instead of removing it from your mod list you can also just deactivate it. You can change that and other things in the mods configuration settings.

When the mod is active it will automatically show all exceptions in a new tab with a Harmony logo, just like in the screenshot above. Each exception will be decoded so it contains the exact mods that are contained in the stacktrace because once this text is exported to a log the meaning is lost and is very hard to reconstruct.

The window has a few functions to help you:

1) clicking on the yellow triangle will suppress this particular exception until you restart RimWorld
2) the blue number counts the number of occurances of this exception and clicking it will reset the count
3) clicking on the white documents icon in the top right will copy the (enhanced) stack trace to your clipboard

Each exception lists all mods in the order of most likely (top) to least likely (bottom). This order **IS ONLY A SUGGESTION** and does not mean that the topmost mod is guaranteed to be blamed. Don't spam the author unnecessary unless you are sure that removing this mod helped. Mod conflicts where multiple mods are affecting each other are possible. The main feature here is to hint you about the involved mods.

# Features

To help you further with restarting your game as few times as possible while searching for mod conflicts, you can access a few other advanced features by clicking on each mod row. The following functions are available in the context menu:

1) If the mod is on steam you can open its steam page by choosing `Workshop page`
2) If a mod has an non-Steam website, you can choose `Click to go to website` to open it
3) Choose `On- > Off` to disable the mod just like unchecking it in the mod list. A restart is still necessary as usual
4) **Advanced user only** Choosing `Additional code off` will unpatch the patch of this method that is involved in this exception. It's not a full unload and will most likely break other stuff but it is a good quick test to remove this particular mod from a repeating exception to see if the exception goes away. This change is only active until RimWorld is restarted and cannot be undone.

# Note

Use this mod wisely, don't bother developers without clear evidence and hopefully this mod will help you to reduce the number of restarts while fixing your mod list a bit. If it just helps you to avoid one extra restart and waiting another 10 minutes to load the game, it will be worth the download/install.

/Brrainz
