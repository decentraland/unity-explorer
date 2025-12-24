namespace MVC
{
    // IExitableState ( Exit only)
    //     ├── IState ( Enter without parameters)
    //     └── IPayloadedState<T> (Enter with parameters)

    public interface IExitableState
    {
        void Exit();
    }

    public interface IState : IExitableState
    {
        void Enter();
    }

    public interface IPayloadedState<in TPayload> : IExitableState
    {
        void Enter(TPayload payload);
    }
}
