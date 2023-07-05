using Arch.Core;
using Arch.SystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Decides if loaded scenes should launched or destroyed
    /// </summary>
    [UpdateInGroup(typeof(SceneLifeCycleGroup))]
    [UpdateAfter(typeof(LoadScenesDynamicallySystem))]
    public partial class ResolveScenesStateSystem : BaseUnityLoopSystem
    {
        private readonly List<string> deleteLiveScenesKeys = new ();

        // cache
        private readonly HashSet<ScenePointer> requiredScenes = new ();
        private readonly SceneLifeCycleState state;

        public ResolveScenesStateSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
        }

        protected override void Update(float t)
        {
            // TODO: load realm-defined scenes to requirements (like Worlds)

            requiredScenes.Clear();

            Vector3 position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

            IReadOnlyList<Vector2Int> parcelsInRange = ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

            foreach ((Vector2Int parcel, ScenePointer pointer) in state.ScenePointers)
            {
                // If Parcel is still in range AND the scene manifest is loaded the scene is a candidate to be launched
                // TODO list.contains is O(n), in the loop is O(m*n)
                if (parcelsInRange.Contains(parcel)
                    && (pointer.IsEmpty || pointer.ManifestPromise.TryGetResult(World, out _)))
                    requiredScenes.Add(pointer);
            }

            // remove scenes that are not required
            foreach ((string id, Entity entity) in state.LiveScenes)
            {
                var deleteScene = true;

                foreach (ScenePointer scenePointer in requiredScenes)
                {
                    if (scenePointer.Definition.id == id) deleteScene = false;
                }

                if (deleteScene)
                {
                    World.Add<DeleteSceneIntention>(entity);
                    deleteLiveScenesKeys.Add(id);
                }
            }

            for (var i = 0; i < deleteLiveScenesKeys.Count; i++)
            {
                string key = deleteLiveScenesKeys[i];
                state.LiveScenes.Remove(key);
            }

            deleteLiveScenesKeys.Clear();

            // create scenes that don't exist
            foreach (ScenePointer pointer in requiredScenes)
            {
                if (pointer.IsEmpty) continue;

                IpfsTypes.SceneEntityDefinition definition = pointer.Definition;
                if (state.LiveScenes.ContainsKey(definition.id)) continue;

                // Launch the scene if the manifest is loaded
                // Don't block the scene if the loading manifest failed, just use NULL
                if (!pointer.ManifestPromise.TryGetResult(World, out StreamableLoadingResult<SceneAssetBundleManifest> manifest))
                {
                    ReportHub.LogError(new ReportData(GetReportCategory(), ReportHint.SessionStatic), $"Asset Bundles Manifest is not loaded for scene {definition.id}");
                    continue;
                }

                pointer.ManifestPromise.Consume(World);

                // this scene is not loaded... we need to load it
                Entity entity = World.Create(new SceneLoadingComponent
                {
                    Definition = definition,
                    AssetBundleManifest = manifest.Succeeded ? manifest.Asset : SceneAssetBundleManifest.NULL,
                });

                state.LiveScenes.Add(definition.id, entity);
            }
        }
    }
}
