using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.Components
{
    [Obsolete("No longer used by Increasing Radius Systems")]
    public readonly struct ParcelsInRange
    {
        public readonly int LoadRadius;

        // Reusable Set of parcels, don't cache it
        [NotNull] public readonly HashSet<int2> Value;

        public ParcelsInRange(HashSet<int2> value, int loadRadius)
        {
            Value = value;
            LoadRadius = loadRadius;
        }
    }
}
