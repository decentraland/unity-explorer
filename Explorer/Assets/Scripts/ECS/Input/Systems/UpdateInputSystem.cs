using Arch.Core;
using CrdtEcsBridge.Components.Special;
using ECS.Abstract;
using ECS.Input.Component;

namespace ECS.Input.Systems
{
    public abstract class UpdateInputSystem<T> : BaseUnityLoopSystem where T : struct, InputComponent
    {

        protected UpdateInputSystem(World world) : base(world)
        {
            World.Query(new QueryDescription().WithAll<PlayerComponent>().WithNone<T>(),
                (in Entity entity) => World.Add<T>(entity));
        }

    }
}
