using System;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    [Serializable]
    public struct AuthScreenEmote
    {
        public string Name;
        public float Duration;
    }

    [CreateAssetMenu(menuName = "DCL/AuthScreen/CharacterPreviewEmotes")]
    public class AuthScreenEmotesData : ScriptableObject
    {
        public AuthScreenEmote StartEmote;
        public AuthScreenEmote JumpInEmote;
        public AuthScreenEmote[] Emotes;
    }
}
