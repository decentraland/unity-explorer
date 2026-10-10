namespace MVC
{
    public interface IReshowController<in TInputData>
    {
        void OnReshowWhileVisible(TInputData inputData);
    }
}
