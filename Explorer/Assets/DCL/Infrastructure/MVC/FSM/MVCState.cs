using System.Threading;

namespace MVC
{
    public abstract class MVCState<TBaseState> where TBaseState: MVCState<TBaseState>
    {
        protected MVCStateMachine<TBaseState> machine;

        protected CancellationToken disposalCt { get; private set; }

        internal void SetMachineAndDisposalCt(MVCStateMachine<TBaseState> machine, CancellationToken disposalCt)
        {
            this.machine = machine;
            this.disposalCt = disposalCt;
            OnInitialized();
        }

        /// <summary>
        ///     called directly after the machine is set allowing the state to do any required setup
        /// </summary>
        public virtual void OnInitialized() { }

        public virtual void Enter() { }

        public virtual void Update(float deltaTime) { }

        public virtual void LateUpdate(float deltaTime) { }

        public virtual void Exit() { }
    }
}
