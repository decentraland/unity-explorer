using CommunicationData.URLHelpers;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.Components
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public AnimationClip? EmoteClip;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
    }
}
