using System;
using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Fixed pointers are created once if <see cref="RealmComponent.ScenesAreFixed" />
    /// </summary>
    public struct FixedScenePointers
    {
        public readonly AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] Promises;

        /// <summary>
        ///     When set, fixed scenes are loaded via GetSceneDefinitionList (e.g. world manifest occupied parcels).
        /// </summary>
        public readonly AssetPromise<SceneDefinitions, GetSceneDefinitionList>? ListPromise;

        // Quick path to avoid an iteration
        public bool AllPromisesResolved;

        public List<SceneEntityDefinition> SceneResults;


        public FixedScenePointers(AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises)
        {
            Promises = promises;
            ListPromise = null;
            AllPromisesResolved = false;
            SceneResults = new List<SceneEntityDefinition>();
        }

        public FixedScenePointers(AssetPromise<SceneDefinitions, GetSceneDefinitionList> listPromise)
        {
            Promises = Array.Empty<AssetPromise<SceneEntityDefinition, GetSceneDefinition>>();
            ListPromise = listPromise;
            AllPromisesResolved = false;
            SceneResults = new List<SceneEntityDefinition>();
        }
    }
}
