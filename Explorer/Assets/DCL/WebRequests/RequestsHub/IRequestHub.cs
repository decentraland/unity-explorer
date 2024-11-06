namespace DCL.WebRequests.RequestsHub
{
    public interface IRequestHub
    {
        InitializeRequest<T, TWebRequest> RequestDelegateFor<T, TWebRequest>()
            where T: struct
            where TWebRequest: struct, ITypedWebRequest;
    }
}
