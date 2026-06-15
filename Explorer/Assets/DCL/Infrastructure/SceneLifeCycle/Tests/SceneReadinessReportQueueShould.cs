using Cysharp.Threading.Tasks;
using DCL.Utilities;
using ECS.SceneLifeCycle.Reporting;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.Tests
{
    [TestFixture]
    public class SceneReadinessReportQueueShould
    {
        private static readonly Vector2Int PARCEL = new (100, 100);

        private IScenesCache scenesCache;
        private SceneReadinessReportQueue queue;

        [SetUp]
        public void SetUp()
        {
            scenesCache = Substitute.For<IScenesCache>();
            queue = new SceneReadinessReportQueue(scenesCache);
        }

        [Test]
        public void KeepReportPendingWhenSceneIsNotLoaded()
        {
            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            queue.Enqueue(PARCEL, report);

            Assert.That(report.GetStatus().TaskStatus, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(queue.TryDequeue(PARCEL, out _), Is.True);
        }

        [Test]
        public void ConcludeReportImmediatelyWhenSceneIsRunning()
        {
            var scene = Substitute.For<ISceneFacade>();
            var stateProvider = Substitute.For<ISceneStateProvider>();
            stateProvider.State = new Atomic<SceneState>(SceneState.Running);
            scene.SceneStateProvider.Returns(stateProvider);

            scenesCache.TryGetByParcel(PARCEL, out Arg.Any<ISceneFacade>())
                       .Returns(x =>
                        {
                            x[1] = scene;
                            return true;
                        });

            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            queue.Enqueue(PARCEL, report);

            Assert.That(report.GetStatus().TaskStatus, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void ConcludeReportImmediatelyWhenNonRealSceneIsAlreadyLoaded()
        {
            // Re-teleporting to an SDK6 (LOD-only) or road scene that is still instantiated:
            // no system would dequeue the report, so it must conclude at enqueue time.
            scenesCache.ContainsNonRealScene(PARCEL).Returns(true);

            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            queue.Enqueue(PARCEL, report);

            Assert.That(report.GetStatus().TaskStatus, Is.EqualTo(UniTaskStatus.Succeeded));
        }
    }
}
