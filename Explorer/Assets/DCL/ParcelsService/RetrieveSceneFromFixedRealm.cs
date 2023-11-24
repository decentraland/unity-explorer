using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public partial class RetrieveSceneFromFixedRealm : IRetrieveScene
    {
        /// <summary>
        ///     World should be set when the realm is [re-]loaded
        /// </summary>
        public World World { private get; set; }

        public async UniTask<IpfsTypes.SceneEntityDefinition> ByParcel(Vector2Int parcel, CancellationToken ct)
        {
            // Wait for all pointers resolution, they should be resolved at start-up

            var resolved = false;
            AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>[] result = null;

            while (!resolved)
            {
                ReadFixedRealmQuery(World, ref resolved, ref result);
                await UniTask.Yield(ct);
            }

            // Check if result contains the requested parcel

            // TODO O(N)
            for (var i = 0; i < result.Length; i++)
            {
                IpfsTypes.SceneEntityDefinition sceneDef = result[i].Result.Value.Asset;

                for (var j = 0; j < sceneDef.metadata.scene.DecodedParcels.Count; j++)
                {
                    if (sceneDef.metadata.scene.DecodedParcels[j] == parcel)
                        return sceneDef;
                }
            }

            // No real scene found
            return null;
        }

        [Query]
        private void ReadFixedRealm([Data] ref bool resolved, [Data] ref AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>[] result,
            in FixedScenePointers fixedScenePointers)
        {
            resolved = fixedScenePointers.AllPromisesResolved;

            if (resolved)
                result = fixedScenePointers.Promises;
        }
    }
}
