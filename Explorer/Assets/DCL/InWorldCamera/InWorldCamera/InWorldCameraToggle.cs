using Arch.Core;
using DCL.CharacterCamera;
using ECS.Abstract;

namespace DCL.InWorldCamera
{
    public class InWorldCameraToggle
    {
        private readonly World world;

        private SingleInstanceEntity? cameraInternal;

        private SingleInstanceEntity? camera => cameraInternal ??= world.CacheCamera();

        public InWorldCameraToggle(World world)
        {
            this.world = world;
        }

        public void Toggle(string source)
        {
            if (!CanChangeActivity()) return;

            world.Add(camera!.Value, new ToggleInWorldCameraRequest
            {
                IsEnable = !world.Has<InWorldCameraComponent>(camera!.Value),
                Source = source,
            });
        }

        public void Disable()
        {
            if (!CanChangeActivity()) return;

            world.Add(camera!.Value, new ToggleInWorldCameraRequest { IsEnable = false });
        }

        private bool CanChangeActivity() =>
            world.Get<CameraComponent>(camera!.Value).CameraInputChangeEnabled
            && !world.Has<ToggleInWorldCameraRequest>(camera!.Value);
    }
}
