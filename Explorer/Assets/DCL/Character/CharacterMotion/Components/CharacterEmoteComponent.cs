using CommunicationData.URLHelpers;
using DCL.Character.CharacterMotion.Emotes;
using UnityEngine;

namespace DCL.CharacterMotion.Components
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
