using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics;
using Diagnostics.ReportsHandling;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene.Tests
{
    [TestFixture]
    public class SceneExceptionHandlerShould
    {
        private ReportHubLogger savedInstance;

        [SetUp]
        public void SetUp()
        {
            sceneExceptionsHandler = SceneExceptionsHandler.Create(sceneStateProvider = Substitute.For<ISceneStateProvider>(), new SceneShortInfo());

            savedInstance = ReportHub.Instance;

            ReportHub.Instance = new ReportHubLogger(
                new List<(ReportHandler, IReportHandler)> { (ReportHandler.DebugLog, reportHandler = Substitute.For<IReportHandler>()) });
        }

        [TearDown]
        public void TearDown()
        {
            ReportHub.Instance = savedInstance;
        }

        private ISceneStateProvider sceneStateProvider;
        private SceneExceptionsHandler sceneExceptionsHandler;
        private IReportHandler reportHandler;

        [Test]
        public void SetStateOnEngineException()
        {
            var e = new Exception("TEST");
            sceneExceptionsHandler.OnEngineException(e, "TEST");

            sceneStateProvider.Received().State = SceneState.EngineError;
            reportHandler.Received().LogException(e, new ReportData("TEST"), null);
        }

        [Test]
        public void TolerateEcsExceptions()
        {
            Type systemGroup = typeof(SimulationSystemGroup);

            for (var i = 0; i < SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE; i++)
            {
                ISystemGroupExceptionHandler.Action action = sceneExceptionsHandler.Handle(new EcsSystemException(Substitute.For<ISystem<float>>(), new ArgumentException("TEST"), new ReportData("TEST")), systemGroup);
                Assert.That(action, Is.EqualTo(ISystemGroupExceptionHandler.Action.Continue));
            }

            sceneStateProvider.DidNotReceive().State = Arg.Any<SceneState>();
        }

        [Test]
        public void SuspendIfToleranceExceeded()
        {
            Type systemGroup = typeof(SimulationSystemGroup);

            ISystemGroupExceptionHandler.Action action = ISystemGroupExceptionHandler.Action.Continue;

            for (var i = 0; i < SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1; i++)
                action = sceneExceptionsHandler.Handle(new EcsSystemException(Substitute.For<ISystem<float>>(), new ArgumentException(i.ToString()), new ReportData("TEST")), systemGroup);

            Assert.That(action, Is.EqualTo(ISystemGroupExceptionHandler.Action.Suspend));
            sceneStateProvider.Received().State = SceneState.EcsError;

            reportHandler.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));
        }
    }
}
