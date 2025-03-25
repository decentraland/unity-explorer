namespace DCL.WebRequests.RequestsHub
{
    public interface IRequestHub
    {
        InitializeRequest<TArgs, TWebRequest> RequestDelegateFor<TArgs, TWebRequest>()
            where TArgs: struct
            where TWebRequest: ITypedWebRequest<TArgs>;
    }
}

__
