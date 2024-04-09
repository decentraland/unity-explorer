using Arch.Core;
using NSubstitute;
using NUnit.Framework;

namespace MVC.Tests
{
    public class BridgeSystemBindingShould
    {
        public abstract class TestSystem : ControllerECSBridgeSystem
        {
            protected TestSystem() : base(World.Create()) { }
        }

        private BridgeSystemBinding<TestSystem> binding;
        private TestSystem system;
        private IController controller;
        private ControllerECSBridgeSystem.QueryMethod queryMethod;


        public void Setup()
        {
            controller = Substitute.For<IController>();
            queryMethod = Substitute.For<ControllerECSBridgeSystem.QueryMethod>();
            system = Substitute.For<TestSystem>();
            binding = new BridgeSystemBinding<TestSystem>(controller, queryMethod);
        }


        public void InvokeQueryOnInjection()
        {
            controller.State.Returns(ControllerState.ViewFocused);

            binding.InjectSystem(system);

            system.Received(1).SetQueryMethod(queryMethod);
            system.Received(1).Activate();
            system.Received(1).InvokeQuery();
        }


        public void NotInvokeQueryOnInjection([Values(ControllerState.ViewBlurred, ControllerState.ViewHidden)] ControllerState state)
        {
            controller.State.Returns(state);

            binding.InjectSystem(system);

            system.Received(1).SetQueryMethod(queryMethod);

            system.DidNotReceive().Activate();
            system.DidNotReceive().InvokeQuery();
        }
    }
}
