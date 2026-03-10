namespace MVC
{
    public abstract class IndependentMVCState
    {
        public bool IsActive { get; private set; }

        public bool TryActivate()
        {
            if (IsActive)
                return false;

            IsActive = true;
            Activate();
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

        public void ReActivate()
        {
            if (IsActive)
                Deactivate();

            IsActive = true;
            Activate();
        }

        protected abstract void Activate();

        protected abstract void Deactivate();
    }
}
