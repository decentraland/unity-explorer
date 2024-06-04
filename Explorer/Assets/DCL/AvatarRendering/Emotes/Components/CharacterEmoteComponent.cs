using CommunicationData.URLHelpers;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public AnimationClip? EmoteClip;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
        public bool StopEmote;

        public void Reset()
        {
            EmoteClip = null;
            EmoteLoop = false;
            CurrentEmoteReference = null;
        }
    }
}
