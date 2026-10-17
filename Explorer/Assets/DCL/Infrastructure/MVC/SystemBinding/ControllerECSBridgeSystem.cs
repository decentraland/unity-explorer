using Arch.Core;
using ECS.Abstract;

namespace MVC
{
    /// <summary>
    ///     System is sub-ordinate to the controller and un-like normal systems
    ///     it can expose data and methods to it.
    ///     It is created in the builder just like a normal system
    /// </summary>
    public abstract class ControllerECSBridgeSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     Query logic is provided by the Controller itself
        /// </summary>
        public delegate void QueryMethod(World world);

        private bool activated;

        private QueryMethod queryMethod;

        protected ControllerECSBridgeSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            if (activated)
                queryMethod(World);
        }

        public void InvokeQuery()
        {
            queryMethod(World);
        }

        public void SetQueryMethod(QueryMethod queryMethod)
        {
            this.queryMethod = queryMethod;
        }

        public void Activate()
        {
            activated = true;
        }

        public void Deactivate()
        {
            activated = false;
        }
    }
}
