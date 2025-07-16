namespace MVC
{
    public abstract class MVCState<TBaseState, TContext> where TBaseState: MVCState<TBaseState, TContext>
    {
        private MVCStateMachine<TBaseState, TContext> machine;

        protected TContext context { get; private set; }

        internal void SetMachineAndContext(MVCStateMachine<TBaseState, TContext> machine, TContext context)
        {
            this.machine = machine;
            this.context = context;
            OnInitialized();
        }

        /// <summary>
        ///     called directly after the machine and context are set allowing the state to do any required setup
        /// </summary>
        public virtual void OnInitialized() { }

        public virtual void Begin() { }

        public virtual void Update(float deltaTime) { }

        public virtual void LateUpdate(float deltaTime) { }

        public virtual void End() { }

        protected R ChangeState<R>() where R: TBaseState =>
            machine.ChangeState<R>();
    }
}
