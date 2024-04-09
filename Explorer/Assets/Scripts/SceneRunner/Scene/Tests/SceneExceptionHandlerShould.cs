using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;

namespace SceneRunner.Scene.Tests
{

    public class SceneExceptionHandlerShould
    {

        public void SetUp()
        {
            sceneExceptionsHandler = SceneExceptionsHandler.Create(sceneStateProvider = Substitute.For<ISceneStateProvider>(), new SceneShortInfo());

            reportHandler = new MockedReportScope();
        }


        public void TearDown()
        {
            reportHandler.Dispose();
        }

        private ISceneStateProvider sceneStateProvider;
        private SceneExceptionsHandler sceneExceptionsHandler;
        private MockedReportScope reportHandler;


        public void SetStateOnEngineException()
        {
            var e = new Exception("TEST");
            sceneExceptionsHandler.OnEngineException(e, "TEST");

            sceneStateProvider.Received().State = SceneState.EngineError;
            reportHandler.Mock.Received().LogException(e, new ReportData("TEST"), null);
        }


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
