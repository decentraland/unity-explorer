using DCL.Character.CharacterMotion.Emotes;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct CharacterEmoteComponent
    {
        public bool WasEmoteJustTriggered;
        public bool EmoteLoop;
        public AnimationClip? EmoteClip;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
    }
}
