namespace MVC
{
    public abstract class IndependentMVCState : IndependentMVCState<ControllerNoData, ControllerNoData>
    {
        protected IndependentMVCState() : base(new ControllerNoData()) { }

        public bool TryActivate() =>
            TryActivate(new ControllerNoData());

        public void Reactivate() =>
            ReActivate(new ControllerNoData());
    }

    public abstract class IndependentMVCState<TContext> : IndependentMVCState<TContext, ControllerNoData>
    {
        protected IndependentMVCState(TContext context) : base(context) { }

        public bool TryActivate() =>
            TryActivate(new ControllerNoData());

        public void ReActivate() =>
            ReActivate(new ControllerNoData());
    }

    public abstract class IndependentMVCState<TContext, TInput>
    {
        protected readonly TContext context;

        protected IndependentMVCState(TContext context)
        {
            this.context = context;
        }

        public bool IsActive { get; private set; }

        public bool TryActivate(TInput input)
        {
            if (IsActive)
                return false;

            IsActive = true;
            Activate(input);
            return true;
        }

        public bool TryDeactivate()
        {
            if (!IsActive)
                return false;

            IsActive = false;
            Deactivate();
            return true;
        }

        public void ReActivate(TInput input)
        {
            if (IsActive)
                Deactivate();

            IsActive = true;
            Activate(input);
        }

        protected abstract void Activate(TInput input);

        protected abstract void Deactivate();
    }
}
