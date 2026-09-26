using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation
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
            List<SceneEntityDefinition> result = null!;

            while (!resolved)
            {
                ReadFixedRealmQuery(World, ref resolved, ref result);
                await UniTask.Yield(ct);
            }

            // Check if result contains the requested parcel
            foreach (var sceneEntityDefinition in result)
            {
                if (sceneEntityDefinition.Contains(parcel))
                    return sceneEntityDefinition;
            }

            // No real scene found
            return null;
        }

        [Query]
        private void ReadFixedRealm([Data] ref bool resolved, [Data] ref List<SceneEntityDefinition> results,
            in FixedScenePointers fixedScenePointers)
        {
            resolved = fixedScenePointers.AllPromisesResolved;

            if (resolved)
                results = fixedScenePointers.SceneResults;
        }
    }
}

