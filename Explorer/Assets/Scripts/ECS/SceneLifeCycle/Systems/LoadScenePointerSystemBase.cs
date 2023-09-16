using Arch.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle.SceneDefinition;
using Ipfs;
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public abstract class LoadScenePointerSystemBase : BaseUnityLoopSystem
    {
        protected LoadScenePointerSystemBase(World world) : base(world) { }

        protected Entity CreateSceneEntity(IpfsTypes.SceneEntityDefinition definition, IpfsTypes.IpfsPath ipfsPath, out Vector2Int[] parcels)
        {
            parcels = new Vector2Int[definition.metadata.scene.parcels.Count];

            for (var i = 0; i < definition.metadata.scene.parcels.Count; i++)
                parcels[i] = IpfsHelper.DecodePointer(definition.metadata.scene.parcels[i]);

            return World.Create(new SceneDefinitionComponent(definition, parcels, ipfsPath));
        }

        /// <summary>
        ///     Creates a scene entity if none of scene parcels were processed yet
        /// </summary>
        protected void TryCreateSceneEntity(IpfsTypes.SceneEntityDefinition definition, IpfsTypes.IpfsPath ipfsPath, NativeHashSet<int2> processedParcels)
        {
            var shouldCreate = true;

            // allocate a final array only if entity should be created, to hold an intermediate array use cheap stackalloc
            Span<Vector2Int> parcels = stackalloc Vector2Int[definition.metadata.scene.parcels.Count];

            for (var i = 0; i < definition.metadata.scene.parcels.Count; i++)
            {
                var parcel = IpfsHelper.DecodePointer(definition.metadata.scene.parcels[i]);
                parcels[i] = parcel;

                if (!processedParcels.Add(parcel.ToInt2()))
                    shouldCreate = false;
            }

            if (shouldCreate)
            {
                // Note: Span.ToArray is not LINQ
                World.Create(new SceneDefinitionComponent(definition, parcels.ToArray(), ipfsPath));
            }
        }
    }
}
