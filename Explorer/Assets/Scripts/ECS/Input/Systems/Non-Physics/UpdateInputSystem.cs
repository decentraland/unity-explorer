using Arch.Core;
using Arch.System;
using ECS.Abstract;
using ECS.Input.Component;
using System.Runtime.CompilerServices;

namespace ECS.Input.Systems
{
    public abstract class UpdateInputSystem<T> : BaseUnityLoopSystem where T : struct, InputComponent
    {

        private static readonly QueryDescription QUERY_INPUT = new QueryDescription().WithAll<T>();
        private readonly Query query;

        protected UpdateInputSystem(World world) : base(world)
        {
            World.Create<T>();
            query = World.Query(in QUERY_INPUT);
        }

        //TODO: Cant I Do queries in abstract classes?
        protected override void Update(float t)
        {
            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                ref T inputFirstElement = ref chunk.GetFirst<T>();
                foreach (int entityIndex in chunk)
                {
                    UpdateInput(ref Unsafe.Add(ref inputFirstElement, entityIndex));
                }
            }
        }

        protected abstract void UpdateInput(ref T inputToUpdate);
    }
}
