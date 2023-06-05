using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Linq;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LoadSceneDynamicallySystem))]
    public partial class SceneLifeCycleSystem : BaseUnityLoopSystem
    {
        private readonly SceneLifeCycleState state;

        // cache
        private readonly HashSet<IpfsTypes.SceneEntityDefinition> requiredScenes = new();

        public SceneLifeCycleSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
        }

        protected override void Update(float t)
        {
            // TODO: load realm-defined scenes to requirements (like Worlds)

            requiredScenes.Clear();

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
            foreach (var (id, entity) in state.LiveScenes)
            {
                var add = true;
                foreach (var definition in requiredScenes)
                {
                    if (definition.id == id)
                    {
                        add = false;
                    }
                }

                if (add)
                {
                    World.Add<DeleteSceneIntention>(entity);
                    state.LiveScenes.Remove(id);
                }
            }

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
                        Definition = definition
                    });

                    state.LiveScenes.Add(definition.id, entity);
                }
            }
        }
    }
}
