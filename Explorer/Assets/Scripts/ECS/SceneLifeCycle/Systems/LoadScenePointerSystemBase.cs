﻿using Arch.Core;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using Ipfs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public abstract class LoadScenePointerSystemBase : BaseUnityLoopSystem
    {
        protected LoadScenePointerSystemBase(World world) : base(world) { }

        protected Entity CreateSceneEntity(SceneEntityDefinition definition, IpfsPath ipfsPath, bool isPortableExperience = false) =>
            World.Create(SceneDefinitionComponentFactory.CreateFromDefinition(definition, ipfsPath, isPortableExperience), new SceneLoadingState());

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
