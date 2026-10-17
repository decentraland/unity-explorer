using JetBrains.Annotations;
using System;

namespace MVC
{
    /// <summary>
    ///     Encapsulates logic for controller that is connected to the system
    /// </summary>
    public class BridgeSystemBinding<TSystem> : IMVCControllerModule where TSystem: ControllerECSBridgeSystem
    {
        private readonly IController controller;
        private readonly ControllerECSBridgeSystem.QueryMethod queryMethod;

        private TSystem system;

        /// <summary>
        ///     This constructor is used if the moment of system's and controller's instantiation is different
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="queryMethod"></param>
        public BridgeSystemBinding(IController controller, ControllerECSBridgeSystem.QueryMethod queryMethod)
        {
            this.controller = controller;
            this.queryMethod = queryMethod;
        }

        /// <summary>
        ///     This constructor is used if the moment of system's and controller's instantiation is the same
        /// </summary>
        public BridgeSystemBinding(IController controller, ControllerECSBridgeSystem.QueryMethod queryMethod, TSystem system)
        {
            this.controller = controller;
            this.queryMethod = queryMethod;

            InjectSystem(system);
        }

        public void InjectSystem([NotNull] TSystem systemInstance)
        {
            if (system != null)
                throw new ArgumentException("System is already injected", nameof(systemInstance));

            system = systemInstance;
            system.SetQueryMethod(queryMethod);

            // In general we don't know at which point of controller's lifecycle the system is created
            if (controller.State != ControllerState.ViewFocused)
                return;

            system.Activate();
            system.InvokeQuery();
        }

        void IMVCControllerModule.OnFocus()
        {
            if (system != null)
            {
                system.Activate();
                system.InvokeQuery();
            }
        }

        void IMVCControllerModule.OnBlur()
        {
            system?.Deactivate();
        }

        void IMVCControllerModule.OnViewShow()
        {
            system?.Activate();
        }

        void IMVCControllerModule.OnViewHide()
        {
            system?.Deactivate();
        }
    }
}
