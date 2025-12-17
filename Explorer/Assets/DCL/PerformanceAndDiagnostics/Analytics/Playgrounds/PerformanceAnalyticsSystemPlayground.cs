using Arch.Core;
using DCL.Analytics.Systems;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PerformanceAndDiagnostics.Analytics.Services;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.Utilities.Extensions;
using ECS;
using Global.AppArgs;
using Global.Versioning;
using UnityEngine;
using Utility.Json;

namespace DCL.PerformanceAndDiagnostics.Analytics.Playgrounds
{
    public class PerformanceAnalyticsSystemPlayground : MonoBehaviour
    {
        [SerializeField] private AnalyticsConfiguration analyticsConfiguration = null!;
        [SerializeField] private BuildData buildData = null!;

        private PerformanceAnalyticsSystem system = null!;

        private void Start()
        {
            system = new PerformanceAnalyticsSystem(
                World.Create(),
                new AnalyticsController(
                    new DebugAnalyticsService(),
                    new ApplicationParametersParser(),
                    analyticsConfiguration.EnsureNotNull(),
                    new LauncherTraits(),
                    buildData.EnsureNotNull(),
                    DCLVersion.Mock()
                ),
                new LoadingStatus(),
                new RealmData(),
                new Profiler(),
                new EntityParticipantTable(),
                new JsonObjectBuilder()
            );
        }

        private void Update()
        {
            system.Update(UnityEngine.Time.deltaTime);
        }
    }
}
