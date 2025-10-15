
using UnityEngine;

namespace DCL.EmotesWheel.Params
{
    public struct EmotesWheelParams
    {
        /// <summary>
        /// Whether the emote (or social emote) is being directed to a specific user. If it is a social emote, only that user
        /// will be able to react.
        /// </summary>
        public bool IsDirectedEmote;
        public string TargetUsername;
        public Color TargetUsernameColor;
    }
}
