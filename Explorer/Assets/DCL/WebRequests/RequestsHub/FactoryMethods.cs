namespace DCL.WebRequests.RequestsHub
{
    /// <summary>
    ///     Creates/Initialize Web Request based on common and specific arguments
    /// </summary>
    public delegate TWebRequest InitializeRequest<TArgs, out TWebRequest>(IWebRequestController controller, in RequestEnvelope<TArgs> requestEnvelope)
        where TArgs: struct
        where TWebRequest: struct, ITypedWebRequest<TArgs>;
}
