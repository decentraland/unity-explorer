using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Late-bound holder for the <see cref="VoiceChatConfiguration"/> ScriptableObject.
    /// Created before InjectToWorld, populated in InitializeAsync after the SO is loaded.
    /// The SO itself is the single source of truth for all proximity audio parameters.
    /// </summary>
    public class ProximityConfigHolder
    {
        public VoiceChatConfiguration? Config;

        /// <summary>
        /// Mouth poses sliced from the atlas. Null until InitializeAsync completes.
        /// </summary>
        public Texture2DArray? MouthTextureArray;

        /// <summary>
        /// Identities of participants currently speaking (from Island Room ActiveSpeakers).
        /// Updated via event subscription; read by the system each frame.
        /// </summary>
        public readonly HashSet<string> SpeakingParticipants = new ();
    }
}
