using DCL.Diagnostics;
using ECS.SceneLifeCycle.Components;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     The logic of loading an empty scene, it requires fewer steps and some data is mocked
    /// </summary>
    public class LoadEmptySceneSystemLogic : IDisposable
    {
        public void Dispose() { }

        public ISceneFacade Flow(GetSceneFacadeIntention intent)
        {
            // pick one of available scenes randomly based on coordinates
            Vector2Int parcel = intent.DefinitionComponent.Parcels[0];

            var emptyScene = EmptySceneFacade.Create(new EmptySceneFacade.Args(new SceneShortInfo(parcel, "EMPTY SCENE")));

            return emptyScene;
        }
    }
}
