using Arch.Core;
using DCL.Analytics.Systems;
using DCL.Profiling;
using DCL.Utilities.Extensions;
using ECS;
using ECS.SceneLifeCycle;
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
                    analyticsConfiguration.EnsureNotNull(),
                    new LauncherTraits()
                ),
                new RealmData(),
                new Profiler(),
                new V8ActiveEngines(),
                new ScenesCache(),
                new JsonObjectBuilder()
            );
        }

        private void Update()
        {
            system.Update(Time.deltaTime);
        }
    }
}
