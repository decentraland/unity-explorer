using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT.Protocol;
using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;

namespace SceneRunner.Scene.Tests
{
    [TestFixture]
    public class SceneExceptionHandlerShould
    {
        [SetUp]
        public void SetUp()
        {
            sceneExceptionsHandler = SceneExceptionsHandler.Create(sceneStateProvider = Substitute.For<ISceneStateProvider>(), new SceneShortInfo());

            reportHandler = new MockedReportScope();
        }

        [TearDown]
        public void TearDown()
        {
            reportHandler.Dispose();
        }

        private ISceneStateProvider sceneStateProvider;
        private SceneExceptionsHandler sceneExceptionsHandler;
        private MockedReportScope reportHandler;

        [Test]
        public void TolerateEngineException()
        {
            for (var i = 0; i < SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE; i++)
            {
                var e = new Exception("TEST");
                sceneExceptionsHandler.OnEngineException(e, "TEST");
            }

            sceneStateProvider.DidNotReceive().State = Arg.Any<SceneState>();
        }

        [Test]
        public void SuspendIfToleranceExceededWhenEngineException()
        {
            for (var i = 0; i < SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1; i++)
                sceneExceptionsHandler.OnEngineException(new Exception("TEST"), "TEST");

            sceneStateProvider.Received().State = SceneState.EcsError;

            reportHandler.Mock.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));

            sceneStateProvider.Received().State = SceneState.EngineError;
        }

        [Test]
        public void TolerateJavascriptException()
        {
            for (var i = 0; i < SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE; i++)
            {
                var e = new Exception("TEST");
                sceneExceptionsHandler.OnJavaScriptException(e);
            }

            sceneStateProvider.DidNotReceive().State = Arg.Any<SceneState>();
        }

        [Test]
        public void SuspendIfToleranceExceededWhenJavascriptException()
        {
            for (var i = 0; i < SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1; i++)
                sceneExceptionsHandler.OnJavaScriptException(new Exception("TEST"));

            sceneStateProvider.Received().State = SceneState.EcsError;

            reportHandler.Mock.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));

            sceneStateProvider.Received().State = SceneState.JavaScriptError;
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

            reportHandler.Mock.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));
        }
    }
}
