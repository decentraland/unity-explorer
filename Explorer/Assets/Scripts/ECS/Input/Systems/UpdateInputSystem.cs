using Arch.Core;
using ECS.Abstract;
using ECS.Input.Component;

namespace ECS.Input.Systems
{
    public abstract class UpdateInputSystem<T, TQueryComponent> : BaseUnityLoopSystem where T: struct, IInputComponent
    {
        protected UpdateInputSystem(World world) : base(world)
        {
            World.Query(new QueryDescription().WithAll<TQueryComponent>().WithNone<T>(),
                (in Entity entity) => World.Add<T>(entity));
        }
    }
}
