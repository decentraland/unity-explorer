using DCL.Utilities;
using System.Collections.Generic;
using Utility;

namespace DCL.UserInAppInitializationFlow
{
    public class RealFlowLoadingStatus : IReadOnlyRealFlowLoadingStatus
    {
        public enum Stage : byte
        {
            Init = 0,
            ProfileLoaded = 1,
            LandscapeLoaded = 2,

            /// <summary>
            ///     Player has teleported to the spawn point of the starting scene
            /// </summary>
            PlayerTeleported = 3,

            Completed = 4,
        }

        public static readonly Dictionary<Stage, float> PROGRESS = new (EnumUtils.GetEqualityComparer<Stage>())
        {
            [Stage.Init] = 0f,
            [Stage.ProfileLoaded] = 0.1f,
            [Stage.LandscapeLoaded] = 0.5f,
            [Stage.PlayerTeleported] = 0.95f,
            [Stage.Completed] = 1f,
        };

        public ReactiveProperty<Stage> CurrentStage { get; } = new (Stage.Init);

        public float SetStage(Stage stage)
        {
            CurrentStage.Value = stage;
            return PROGRESS[stage];
        }
    }
}
