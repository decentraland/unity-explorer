using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using UnityEngine;

namespace ECS.Unity.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateTransformUnitySystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, Transform>();
        private UpdateTransform updateTransform;

        public UpdateTransformUnitySystem(World world) : base(world)
        {
            updateTransform = new UpdateTransform();
        }

        protected override void Update(float _)
        {
            World.InlineEntityQuery<UpdateTransform, SDKTransform, Transform>(in queryDescription, ref updateTransform);
        }

        private readonly struct UpdateTransform : IForEachWithEntity<SDKTransform, Transform>
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
