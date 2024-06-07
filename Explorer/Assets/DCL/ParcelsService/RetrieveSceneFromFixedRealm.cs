using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System.Threading;
using UnityEngine;

namespace DCL.ParcelsService
{
    public partial class RetrieveSceneFromFixedRealm : IRetrieveScene
    {
        /// <summary>
        ///     World should be set when the realm is [re-]loaded
        /// </summary>
        public World? World { private get; set; }

        public async UniTask<SceneEntityDefinition?> ByParcelAsync(Vector2Int parcel, CancellationToken ct)
        {
            // Wait for all pointers resolution, they should be resolved at start-up

            var resolved = false;
            AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] result = null!;

            while (!resolved)
            {
                ReadFixedRealmQuery(World, ref resolved, ref result);
                await UniTask.Yield(ct);
            }

            // Check if result contains the requested parcel
            // TODO O(N)
            foreach (var sceneDefPromise in result)
            {
                if (!sceneDefPromise.Result.HasValue) continue;
                if (!sceneDefPromise.Result!.Value.Succeeded) continue;

                SceneEntityDefinition? sceneDef = sceneDefPromise.Result!.Value.Asset;

                for (var j = 0; j < sceneDef?.metadata.scene.DecodedParcels.Count; j++)
                    if (sceneDef.metadata.scene.DecodedParcels[j] == parcel)
                        return sceneDef;
            }

            // No real scene found
            return null;
        }

        [Query]
        private void ReadFixedRealm([Data] ref bool resolved, [Data] ref AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] result,
            in FixedScenePointers fixedScenePointers)
        {
            resolved = fixedScenePointers.AllPromisesResolved;

            if (resolved)
                result = fixedScenePointers.Promises;
        }
    }
}
