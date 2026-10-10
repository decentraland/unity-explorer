using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Diagnostics.Tests;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using Utility.Multithreading;

namespace SceneRunner.Scene.Tests
{
    [TestFixture]
    public class SceneExceptionHandlerShould
    {
        private ISceneStateProvider sceneStateProvider;
        private SceneExceptionsHandler sceneExceptionsHandler;
        private MockedReportScope reportHandler;

        [SetUp]
        public void SetUp()
        {
            sceneExceptionsHandler = SceneExceptionsHandler.Create(sceneStateProvider = Substitute.For<ISceneStateProvider>(), new SceneShortInfo());
            sceneStateProvider.State = new Atomic<SceneState>(SceneState.NotStarted);

            reportHandler = new MockedReportScope();
        }

        [TearDown]
        public void TearDown()
        {
            reportHandler.Dispose();
        }

        [Test]
        public void TolerateEngineException()
        {
            for (var i = 0; i < SceneExceptionsHandler.ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE; i++)
            {
                var e = new Exception("TEST");
                sceneExceptionsHandler.OnEngineException(e, "TEST");
            }

            Assert.That(sceneStateProvider.State.Value(), Is.EqualTo(SceneState.NotStarted));
        }

        [Test]
        public void SuspendIfToleranceExceededWhenEngineException()
        {
            for (var i = 0; i < SceneExceptionsHandler.ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1; i++)
                sceneExceptionsHandler.OnEngineException(new Exception("TEST"), "TEST");


            reportHandler.Mock.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));

            Assert.That(sceneStateProvider.State.Value(), Is.EqualTo(SceneState.EngineError));
        }

        [Test]
        public void TolerateJavascriptException()
        {
            for (var i = 0; i < SceneExceptionsHandler.JAVASCRIPT_EXCEPTIONS_PER_MINUTE_TOLERANCE; i++)
            {
                var e = new Exception("TEST");
                sceneExceptionsHandler.OnJavaScriptException(e);
            }

            Assert.That(sceneStateProvider.State.Value(), Is.EqualTo(SceneState.NotStarted));
        }

        [Test]
        public void SuspendIfToleranceExceededWhenJavascriptException()
        {
            for (var i = 0; i < SceneExceptionsHandler.JAVASCRIPT_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1; i++)
                sceneExceptionsHandler.OnJavaScriptException(new Exception("TEST"));

            reportHandler.Mock.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.JAVASCRIPT_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));

            Assert.That(sceneStateProvider.State.Value(), Is.EqualTo(SceneState.JavaScriptError));

        }

        [Test]
        public void TolerateEcsExceptions()
        {
            Type systemGroup = typeof(SimulationSystemGroup);

            for (var i = 0; i < SceneExceptionsHandler.ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE; i++)
            {
                ISystemGroupExceptionHandler.Action action = sceneExceptionsHandler.Handle(new EcsSystemException(Substitute.For<ISystem<float>>(), new ArgumentException("TEST"), new ReportData("TEST")), systemGroup);
                Assert.That(action, Is.EqualTo(ISystemGroupExceptionHandler.Action.Continue));
            }

            Assert.That(sceneStateProvider.State.Value(), Is.EqualTo(SceneState.NotStarted));
        }

        [Test]
        public void SuspendIfToleranceExceeded()
        {
            Type systemGroup = typeof(SimulationSystemGroup);

            ISystemGroupExceptionHandler.Action action = ISystemGroupExceptionHandler.Action.Continue;

            for (var i = 0; i < SceneExceptionsHandler.ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1; i++)
                action = sceneExceptionsHandler.Handle(new EcsSystemException(Substitute.For<ISystem<float>>(), new ArgumentException(i.ToString()), new ReportData("TEST")), systemGroup);

            Assert.That(action, Is.EqualTo(ISystemGroupExceptionHandler.Action.Suspend));
            Assert.That(sceneStateProvider.State.Value(), Is.EqualTo(SceneState.EcsError));

            reportHandler.Mock.Received(1)
                         .LogException(
                              Arg.Is<SceneExecutionException>(e => e.InnerExceptions.Count == SceneExceptionsHandler.ENGINE_EXCEPTIONS_PER_MINUTE_TOLERANCE + 1));
        }
    }
}
