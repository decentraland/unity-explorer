using System;
using System.Collections.Generic;
using Utility;

namespace DCL.UserInAppInitializationFlow
{
    public class RealFlowLoadingStatus : IReadOnlyRealFlowLoadingStatus
    {
        public enum Stage : byte
        {
            ProfileLoaded = 0,
            LandscapeLoaded = 1,

            /// <summary>
            ///     Player has teleported to the spawn point of the starting scene
            /// </summary>
            PlayerTeleported = 2,

            Completed = 3,
        }

        public static readonly Dictionary<Stage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<Stage>())
        {
            [Stage.ProfileLoaded] = 0.1f, [Stage.LandscapeLoaded] = 0.5f,
            [Stage.PlayerTeleported] = 0.95f,
            [Stage.Completed] = 1f,
        };

        public Stage CurrentStage { get; private set; }

        public event Action<Stage>? StageChanged;

        public float SetStage(Stage stage)
        {
            CurrentStage = stage;
            StageChanged?.Invoke(stage);

            return PROGRESS[stage];
        }
    }
}
