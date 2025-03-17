using DCL.CharacterCamera;
using DCL.Interaction.PlayerOriginated;
using ECS.Prioritization.Components;

namespace Global
{
    /// <summary>
    ///     Holds dependencies that are written into in the Global World
    ///     and then propagated to Scene Worlds as readonly
    /// </summary>
    public class ExposedGlobalDataContainer
    {
        public CameraSamplingData CameraSamplingData { get; private set; }

        public ExposedCameraData ExposedCameraData { get; private set; }

        public GlobalInputEvents GlobalInputEvents { get; private set; }

        public static ExposedGlobalDataContainer Create() =>
            new ()
            {
                CameraSamplingData = new CameraSamplingData(),
                ExposedCameraData = new ExposedCameraData(),
                GlobalInputEvents = new GlobalInputEvents(),
            };
    }
}
