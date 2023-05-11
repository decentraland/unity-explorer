using Arch.Core;
using Arch.Core.Extensions;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UpdateTransformSystem : BaseUnityLoopSystem
{
    private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, Transform>();
    private CleanTransform cleanTransform;

    public UpdateTransformSystem(World world) : base(world)
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

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(UpdateTransformSystem))]
public partial class InstantiateUnityTransforms : BaseUnityLoopSystem
{
    private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform>().WithNone<Transform>();
    private TransformInstantiator cleanTransform;

    protected override void Update(float _)
    {
        World.InlineEntityQuery<TransformInstantiator, SDKTransform>(in queryDescription, ref cleanTransform);
    }

    public InstantiateUnityTransforms(World world) : base(world)
    {
        cleanTransform = new TransformInstantiator();
    }

    private readonly struct TransformInstantiator : IForEachWithEntity<SDKTransform>
    {
        public void Update(in Entity entity, ref SDKTransform t0)
        {
            var emptyGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            emptyGameObject.name = $"Entity {entity.Id}";
            entity.Add(emptyGameObject.transform);
        }
    }
}
