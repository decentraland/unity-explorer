using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using Decentraland.Terrain;
using ECS.Abstract;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public sealed partial class CollideTerrainSystem : BaseUnityLoopSystem
    {
        private readonly TerrainColliderState state;
        private readonly List<float2> userPositionsXZ;

        private CollideTerrainSystem(World world, TerrainData terrainData, Transform terrainParent)
            : base(world)
        {
            state = new TerrainColliderState(terrainData, terrainParent);
            userPositionsXZ = new List<float2>();
        }

        protected override void Update(float t)
        {
            userPositionsXZ.Clear();
            GatherUserPositionsQuery(World);
            TerrainCollider.Update(state, userPositionsXZ);
        }

        [Query, All(typeof(PlayerComponent))]
        private void GatherUserPositions(CharacterTransform transform)
        {
            Vector3 position = transform.Position;
            userPositionsXZ.Add(float2(position.x, position.z));
        }
    }
}
