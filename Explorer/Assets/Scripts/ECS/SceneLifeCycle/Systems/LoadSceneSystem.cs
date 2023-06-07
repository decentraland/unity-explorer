using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LoadScenesDynamicallySystem))]
    public partial class LoadSceneSystem : BaseUnityLoopSystem
    {
        // cache
        private readonly HashSet<IpfsTypes.SceneEntityDefinition> requiredScenes = new ();
        private readonly SceneLifeCycleState state;

        public LoadSceneSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
        }

        protected override void Update(float t)
        {
            // TODO: load realm-defined scenes to requirements (like Worlds)

            requiredScenes.Clear();

            Vector3 position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

            List<Vector2Int> parcelsInRange = ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

            foreach ((Vector2Int parcel, IpfsTypes.SceneEntityDefinition definition) in state.ScenePointers)
            {
                if (parcelsInRange.Contains(parcel)) { requiredScenes.Add(definition); }
            }

            // remove scenes that are not required
            foreach (string id in state.LiveScenes.Keys.ToList())
            {
                Entity entity = state.LiveScenes[id];
                var add = true;

                foreach (IpfsTypes.SceneEntityDefinition definition in requiredScenes)
                {
                    if (definition.id == id) { add = false; }
                }

                if (add)
                {
                    World.Add<DeleteSceneIntention>(entity);
                    state.LiveScenes.Remove(id);
                }
            }

            // create scenes that not exists
            foreach (IpfsTypes.SceneEntityDefinition definition in requiredScenes)
            {
                if (state.LiveScenes.ContainsKey(definition.id)) continue;

                // TODO: Remove this code, we must handle the empty-parcels in a different way
                if (definition.id.StartsWith("empty-parcel"))
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
