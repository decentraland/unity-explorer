namespace DCL.WebRequests.RequestsHub
{
    /// <summary>
    ///     Creates/Initialize Web Request based on common and specific arguments
    /// </summary>
    public delegate TWebRequest InitializeRequest<T, out TWebRequest>(string effectiveUrl, ref T specificArguments)
        where T: struct
        where TWebRequest: struct, ITypedWebRequest;
}
