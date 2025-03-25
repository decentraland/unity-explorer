namespace DCL.WebRequests.RequestsHub
{
    /// <summary>
    ///     Creates/Initialize Web Request based on common and specific arguments
    /// </summary>
    public delegate TWebRequest InitializeRequest<TArgs, out TWebRequest>(IWebRequestController controller, in RequestEnvelope requestEnvelope, in TArgs args)
        where TArgs: struct
        where TWebRequest: ITypedWebRequest<TArgs>;
}
