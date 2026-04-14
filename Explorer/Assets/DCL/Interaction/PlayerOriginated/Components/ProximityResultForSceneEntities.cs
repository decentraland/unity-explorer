using DCL.Interaction.Utility;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

namespace DCL.Interaction.PlayerOriginated.Components
{
    public sealed class ProximityResultForSceneEntities
    {
        public GlobalColliderSceneEntityInfo? EntityInfo { get; private set; }
        public Collider? Collider { get; private set; }
        public float DistanceToPlayer { get; private set; }

        public ProximityResultForSceneEntities()
        {
            Reset();
        }

        public void Reset()
        {
            EntityInfo = null;
            Collider = null;
            DistanceToPlayer = float.MaxValue;
        }

        public void Set(
            GlobalColliderSceneEntityInfo entityInfo,
            Collider collider,
            float distanceToPlayer
        )
        {
            EntityInfo = entityInfo;
            Collider = collider;
            DistanceToPlayer = distanceToPlayer;
        }
    }
}
