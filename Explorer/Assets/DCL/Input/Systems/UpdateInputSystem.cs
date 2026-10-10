using Arch.Core;
using DCL.Input.Component;
using ECS.Abstract;

namespace DCL.Input.Systems
{
    public abstract class UpdateInputSystem<T, TQueryComponent> : BaseUnityLoopSystem where T: struct, IInputComponent
    {
        protected UpdateInputSystem(World world) : base(world) { }

        public override void Initialize()
        {
            // We need to execute it on initialize to be independent from the entities with <TQueryComponent> creation order
            World.Query(in new QueryDescription().WithAll<TQueryComponent>().WithNone<T>(),
                entity => World.Add<T>(entity));
        }
    }
}
