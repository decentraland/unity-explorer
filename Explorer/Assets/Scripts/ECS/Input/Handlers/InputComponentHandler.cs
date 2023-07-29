using Arch.Core;
using ECS.Input.Component;

namespace ECS.Input.Handler
{
    public interface InputComponentHandler
    {
        void HandleInput(World world, Entity playerEntity, float dt);
    }

    public interface InputComponentHandler<T> : InputComponentHandler where T : struct, InputComponent
    {
        void HandleInput(float t, ref T component);

        void InputComponentHandler.HandleInput(World world, Entity playerEntity, float dt) =>
            HandleInput(dt, ref world.Get<T>(playerEntity));
    }
}
