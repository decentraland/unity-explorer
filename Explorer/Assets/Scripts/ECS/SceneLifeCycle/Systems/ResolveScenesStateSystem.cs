using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
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
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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
                if (parcelsInRange.Contains(parcel)
                    && (pointer.ManifestPromise == null || pointer.ManifestPromise.Value.TryConsume(World, out _)))
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

            foreach (string key in deleteLiveScenesKeys) state.LiveScenes.Remove(key);

            deleteLiveScenesKeys.Clear();

            // create scenes that don't exist
            foreach (ScenePointer pointer in requiredScenes)
            {
                IpfsTypes.SceneEntityDefinition definition = pointer.Definition;
                if (state.LiveScenes.ContainsKey(definition.id)) continue;

                // TODO: Remove this code, we must handle the empty-parcels in a different way
                if (definition.id.StartsWith("empty-parcel"))
                    continue;

                // Launch the scene if the manifest is loaded
                // TODO report if the manifest failed to load only once
                if (!pointer.ManifestPromise.Value.TryGetResult(World, out StreamableLoadingResult<SceneAssetBundleManifest> manifest) || !manifest.Succeeded)
                    continue;

                // this scene is not loaded... we need to load it
                Entity entity = World.Create(new SceneLoadingComponent
                {
                    Definition = definition,
                });

                state.LiveScenes.Add(definition.id, entity);
            }
        }
    }
}
