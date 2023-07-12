using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ECS.SceneLifeCycle.Components
{
    public readonly struct ParcelsInRange
    {
        public readonly int LoadRadius;

        // Reusable Set of parcels, don't cache it
        [NotNull] public readonly HashSet<Vector2Int> Value;

        public ParcelsInRange(HashSet<Vector2Int> value, int loadRadius)
        {
            Value = value;
            LoadRadius = loadRadius;
        }
    }
}
