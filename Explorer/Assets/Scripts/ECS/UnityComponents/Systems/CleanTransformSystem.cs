using Arch.Core;
using CrdtEcsBridge.Components.Transform;
using ECS.Abstract;

public class CleanTransformSystem : BaseUnityLoopSystem
{
    private readonly QueryDescription queryDescription = new QueryDescription().WithAll<SDKTransform, UnityTransformReferenceComponent, IsDirtyState>();
    private CleanTransform cleanTransform;

    protected override void Update(float _)
    {
        World.InlineEntityQuery<CleanTransform, SDKTransform, UnityTransformReferenceComponent, IsDirtyState>(in queryDescription, ref cleanTransform);
        var query = World.Query(in queryDescription);
    }

    public CleanTransformSystem(World world) : base(world)
    {
        cleanTransform = new CleanTransform();
    }

    private readonly struct CleanTransform : IForEachWithEntity<SDKTransform, UnityTransformReferenceComponent, IsDirtyState>
    {

        public void Update(in Entity entity, ref SDKTransform sdkTransform, ref UnityTransformReferenceComponent unityTransformReference, ref IsDirtyState isDirtyState)
        {
            if (!isDirtyState.hasBeenCleaned)
            {
                var unityTransform = unityTransformReference.reference.transform;
                unityTransform.position = sdkTransform.Position;
                unityTransform.rotation = sdkTransform.Rotation;
                unityTransform.localScale = sdkTransform.Position;
            }
        }
    }
}
