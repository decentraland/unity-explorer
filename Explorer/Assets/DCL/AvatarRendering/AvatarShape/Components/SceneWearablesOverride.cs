using CommunicationData.URLHelpers;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Scene wearables the current scene puts on a player's avatar on top of the profile wearables; empty <see cref="Urns" /> means none.
    /// </summary>
    public struct SceneWearablesOverride
    {
        public readonly List<URN> Urns;
        public bool IsDirty;

        public SceneWearablesOverride(List<URN> urns)
        {
            Urns = urns;
            IsDirty = false;
        }
    }
}
