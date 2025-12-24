namespace MVC
{
    public abstract class MVCState<TBaseState> where TBaseState: MVCState<TBaseState>
    {
        public virtual void Enter() { }
        public virtual void Exit() { }
    }
}
