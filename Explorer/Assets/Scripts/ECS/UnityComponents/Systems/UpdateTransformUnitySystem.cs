using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using UnityEngine;

namespace ECS.UnitySystems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateTransformUnitySystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, Transform>();
        private CleanTransform cleanTransform;

        public UpdateTransformUnitySystem(World world) : base(world)
        {
            cleanTransform = new CleanTransform();
        }

        protected override void Update(float _)
        {
            World.InlineEntityQuery<CleanTransform, SDKTransform, Transform>(in queryDescription, ref cleanTransform);
        }

        private readonly struct CleanTransform : IForEachWithEntity<SDKTransform, Transform>
        {
            public void Update(in Entity entity, ref SDKTransform sdkTransform, ref Transform unityTransform)
            {
                if (sdkTransform.IsDirty)
                {
                    unityTransform.position = sdkTransform.Position;
                    unityTransform.rotation = sdkTransform.Rotation;
                    unityTransform.localScale = sdkTransform.Scale;
                    sdkTransform.IsDirty = false;
                }
            }
        }
    }
}
