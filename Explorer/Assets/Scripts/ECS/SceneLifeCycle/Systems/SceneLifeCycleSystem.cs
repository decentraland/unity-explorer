using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SceneDynamicLoaderSystem))]
    public partial class SceneLifeCycleSystem : BaseUnityLoopSystem
    {
        private SceneLifeCycleState state;

        public SceneLifeCycleSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
        }

        protected override void Update(float t)
        {
            // TODO: load realm-defined scenes to requirements (like Worlds)

            HashSet<Ipfs.EntityDefinition> requiredScenes = new();

            var position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

            var parcelsInRange = ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

            foreach (var (parcel, definition) in state.ScenePointers)
            {
                if (parcelsInRange.Contains(parcel))
                {
                    requiredScenes.Add(definition);
                }
            }

            // remove scenes that are not required
            /*foreach (var (id, entity) in state.LiveScenes)
            {
                if (!requiredScenes.Any(definition => definition.id == id))
                {
                    // TODO: Remove scene
                }
            }*/

            // create scenes that not exists
            foreach (var definition in requiredScenes)
            {
                if (!state.LiveScenes.ContainsKey(definition.id))
                {
                    // TODO: Remove this code, we must handle the empty-parcels in a different way
                    if (definition.id.StartsWith("empty-parcel"))
                        continue;

                    // this scene is not loaded... we need to load it
                    var entity = World.Create(new SceneLoadingComponent()
                    {
                        Definition = definition,
                        State = SceneLoadingState.Spawned
                    });

                    state.LiveScenes.Add(definition.id, entity);
                }
            }
        }
    }
}
