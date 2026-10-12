using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.VoiceChatModifierArea.Components
{
    /// <summary>
    ///     Avatars inside the area that also stand on the scene's parcels, and so carry its voice modifiers.
    /// </summary>
    public readonly struct VoiceChatModifierAreaComponent
    {
        public readonly HashSet<Collider> AffectedAvatars;

        public VoiceChatModifierAreaComponent(HashSet<Collider> affectedAvatars)
        {
            AffectedAvatars = affectedAvatars;
        }
    }
}
