using Arch.Core;
using DCL.Analytics.Systems;
using DCL.PerformanceAndDiagnostics.Analytics.Services;
using DCL.Profiling;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using Global.AppArgs;
using Global.Versioning;
using SceneRuntime;
using Segment.Serialization;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.Playgrounds
{
    public class PerformanceAnalyticsSystemPlayground : MonoBehaviour
    {
        [SerializeField] private AnalyticsConfiguration analyticsConfiguration = null!;

        private PerformanceAnalyticsSystem system = null!;

        private void Start()
        {
            system = new PerformanceAnalyticsSystem(
                World.Create(),
                new AnalyticsController(
                    new DebugAnalyticsService(),
                    new UserIPAddressService(IWebRequestController.DEFAULT),
                    new ApplicationParametersParser(),
                    analyticsConfiguration.EnsureNotNull(),
                    new LauncherTraits(),
                    new BuildData(),
                    DCLVersion.Mock()
                ),
                new RealmData(),
                new Profiler(),
                new V8ActiveEngines(),
                new ScenesCache(),
                new Utility.Json.JsonObjectBuilder()
            );
        }

        private void Update()
        {
            system.Update(Time.deltaTime);
        }
    }
}
