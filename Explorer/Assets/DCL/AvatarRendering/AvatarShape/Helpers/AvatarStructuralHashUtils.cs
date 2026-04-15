using DCL.AvatarRendering.Loading.Components;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class AvatarStructuralHashUtils
    {
        /// <summary>
        /// Computes a hash that identifies the structural identity of an avatar (body shape + equipped wearables).
        /// Used to detect whether a change requires full re-instantiation or is purely cosmetic.
        /// </summary>
        public static int ComputeStructuralHash<T>(BodyShape bodyShape, IEnumerable<T> wearables, bool showOnlyWearables = false)
        {
            int hash = HashCode.Combine(bodyShape.Value, showOnlyWearables);

            foreach (T wearable in wearables)
                hash = HashCode.Combine(hash, wearable);

            return hash;
        }
    }
}
