using CommunicationData.URLHelpers;

namespace DCL.AvatarRendering.Emotes
{
    public struct CharacterEmoteComponent
    {
        public URN EmoteUrn;
        public bool EmoteLoop;
        public EmoteReferences? CurrentEmoteReference;
        public int CurrentAnimationTag;
        public bool StopEmote;

        public bool IsPlayingEmote => CurrentEmoteReference;
        public float PlayingEmoteDuration => CurrentEmoteReference?.avatarClip? CurrentEmoteReference.avatarClip.length : 0f;

        public void Reset()
        {
            EmoteLoop = false;
            CurrentEmoteReference = null;
            StopEmote = false;
        }
    }
}
