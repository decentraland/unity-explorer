using Arch.Core;
using DCL.Ipfs;
using DCL.Roads.Components;
using DCL.SceneRunner.Scene;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
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
        protected readonly IRealmData realmData;

        protected LoadScenePointerSystemBase(World world, HashSet<Vector2Int> roadCoordinates, IRealmData realmData) : base(world)
        {
            this.roadCoordinates = roadCoordinates;
            this.realmData = realmData;
        }

        protected Entity CreateSceneEntity(SceneEntityDefinition definition, IpfsPath ipfsPath, bool isPortableExperience = false, ISSDescriptor? initialISSDescriptor = null)
        {
            // Scene types that structurally cannot have ISS (PX content, static-pointer / LSD scenes,
            // smart-wearable previews) start with a State.None descriptor so the radius resolver gate
            // never fires for them. PX defaults to None automatically; non-PX defaults to Uninitialized
            // and resolves on first LOD/Scene transition. Callers can override with `initialISSDescriptor`.
            ISSDescriptor descriptor = initialISSDescriptor
                                       ?? (isPortableExperience
                                           ? new ISSDescriptor(IISSDescriptor.State.None, default)
                                           : new ISSDescriptor());

            if (IsRoad(definition))
                return World.Create(SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, isPortableExperience), descriptor, RoadInfo.Create(), SceneLoadingState.CreateRoad());

            return World.Create(SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, isPortableExperience), descriptor);
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
