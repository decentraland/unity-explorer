using Arch.Core;
using DCL.Ipfs;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using Ipfs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public abstract class LoadScenePointerSystemBase : BaseUnityLoopSystem
    {
        private readonly HashSet<Vector2Int> roadCoordinates;
        private readonly IRealmData realmData;

        protected LoadScenePointerSystemBase(World world, HashSet<Vector2Int> roadCoordinates, IRealmData realmData) : base(world)
        {
            this.roadCoordinates = roadCoordinates;
            this.realmData = realmData;
        }

        protected Entity CreateSceneEntity(SceneEntityDefinition definition, IpfsPath ipfsPath, bool isPortableExperience = false)
        {
            if (IsRoad(definition))
                return World.Create(SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, isPortableExperience), RoadInfo.Create(), SceneLoadingState.CreateRoad());

            return World.Create(SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, isPortableExperience));
        }

        private bool IsRoad(SceneEntityDefinition definition) =>
            realmData.IsGenesis() && roadCoordinates.Contains(definition.metadata.scene.DecodedBase);

        /// <summary>
        ///     Creates a scene entity if none of scene parcels were processed yet
        /// </summary>
        protected void TryCreateSceneEntity(SceneEntityDefinition definition, IpfsPath ipfsPath, NativeHashSet<int2> processedParcels)
        {
            var shouldCreate = true;

            for (var i = 0; i < definition.metadata.scene.DecodedParcels.Count; i++)
            {
                Vector2Int parcel = definition.metadata.scene.DecodedParcels[i];

                if (!processedParcels.Add(parcel.ToInt2()))
                    shouldCreate = false;
            }

            if (shouldCreate)
            {
                // Note: Span.ToArray is not LINQ
                CreateSceneEntity(definition, ipfsPath);
            }
        }
    }
}
