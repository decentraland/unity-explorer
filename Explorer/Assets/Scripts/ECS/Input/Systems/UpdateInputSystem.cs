using Arch.Core;
using ECS.Abstract;
using ECS.Input.Component;

namespace ECS.Input.Systems
{
    public abstract class UpdateInputSystem<T, TQueryComponent> : BaseUnityLoopSystem where T: struct, IInputComponent
    {
        protected UpdateInputSystem(World world) : base(world) { }

        public override void Initialize()
        {
            // We need to execute it on initialize to be independent from the entities with <TQueryComponent> creation order
            World.Query(new QueryDescription().WithAll<TQueryComponent>().WithNone<T>(),
                (in Entity entity) => World.Add<T>(entity));
        }
    }
}
