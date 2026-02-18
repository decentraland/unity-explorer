using Arch.Core;
using Arch.System;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
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
            SceneEntityDefinition? startupScene = null;

            while (!resolved)
            {
                ReadFixedRealmQuery(World, ref resolved, ref startupScene);
                await UniTask.Yield(ct);
            }

            return startupScene;
        }

        [Query]
        private void ReadFixedRealm([Data] ref bool resolved,
            [Data] ref SceneEntityDefinition startupScene, in FixedScenePointers fixedScenePointers)
        {
            resolved = fixedScenePointers.AllPromisesResolved;

            if (resolved)
                startupScene = fixedScenePointers.StartupScene;
        }
    }
}
