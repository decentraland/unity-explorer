namespace MVC
{
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
