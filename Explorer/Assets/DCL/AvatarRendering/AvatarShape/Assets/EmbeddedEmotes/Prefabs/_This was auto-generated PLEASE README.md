# Summary
Since in unity-explorer we decided to use non-legacy animations in order to be able to do proper AnimatorComponents, we also had to create a system that converts all base emotes into "extended" emotes, so each emote has their own game object with a pre-configured animator with transitions, they do not contain prop animations but they should work as the animations that come from the Asset Bundle Converter

# How to update them
In order to update these prefabs and clip references, just modify the folder that contains all the clips then run 'Decentraland > Generate Embedded Emote Prefabs'
Once the process is finished, update the EmbeddedEmotes scriptable object accordingly.

# Known issues
This process wont delete old clips that are no longer at the Animations folder
If you empty the folder for a clean generation, you will loose all the references at the EmbeddedEmotes ScriptableObject

# Support
Contact Kinerius