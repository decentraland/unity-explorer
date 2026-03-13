# Chat Emojis

This page has the goal of documenting the process of creating all the needed assets in order to show emojis in the explorer chat.

## Sprite atlas creation

First of all, we are using the Google font `noto-emoji`. Its repo is located [here](https://github.com/googlefonts/noto-emoji).

Here's a list of steps needed to create the TMP sprite atlas:

1. Download the noto font in order to get the pngs
2. Rename all files inside the folder `png` in order to remove the `emoji_u` prefix
   - Powershell:
     ```powershell
     Get-Item .\*.* | ForEach-Object { Rename-Item $_ ($_.Name -replace "emoji_u", "") }
     ```
3. Create the texture atlas with [Texture Packer](https://www.codeandweb.com/texturepacker), with the following settings (advanced)
   - Data format: JSON array
   - Trim sprite names: Yes
   - Algorithm: Grid/Strip
4. Edit the resultant json file in order to add the pivot points: replace `({"w":\d+,"h":\d+})` with `$1, \t"pivot": {"x":0,"y":1}`
5. In Unity, use the TMP sprite importer and tick `Use filenames as Unicode` to generate the asset
6. (this is not needed as it is already done) In Unity's project settings, set the emoji fallback asset in the TMP settings
7. Download and import in the project (file should be located in `EmojiPanel/Editor/`) the emoji json metadata that can be found [here](https://github.com/googlefonts/emoji-metadata). Currently using version 15
8. To map emojis to their shortcodes
   - locate the file `EmojiPanelConfig.asset`
   - link the noto sprite atlas and the json metadata just described if missing
   - click on `Load Definitions`
   - after some seconds an output log will be outputted in the console
   - all UTF-16 code points will be automatically extracted and mapped in the asset (keep in mind that the json definition contains definitions that are not currently representable as their unicode value must be between 0x000000 and 0x10ffff, inclusive, and should not include surrogate codepoint values (0x00d800 ~ 0x00dfff))

After doing these steps you should be able to see and use the emojis.
