using DCL.Diagnostics;
using REnum;

namespace DCL.RuntimeDeepLink
{
    [REnum]
    [REnumField(typeof(DeepLinkHandleImplementation))]
    [REnumFieldEmpty("Null")]
    public partial struct DeepLinkHandle
    {
        public string Name => Match(onDeepLinkHandleImplementation: static _ => "Real Implementation", onNull: static () => "Null Implementation");

        /// <summary>
        /// Implementations of the method must be exception free.
        /// </summary>
        public void HandleDeepLink(DeepLink deeplink)
        {
            HandleResult result = Match(
                deeplink,
                onDeepLinkHandleImplementation: static (deeplink, handle) => handle.HandleDeepLink(deeplink),
                onNull: static _ => HandleResult.Ok()
            );

            result.Match(
                (Name, deeplink),
                onOk: static tuple => ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"{tuple.Name} successfully handled deeplink: {tuple.deeplink}"),
                onHandleError: static (tuple, error) => ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"{tuple.Name} raised error on handle deeplink: {tuple.deeplink}, error {error.Message}")
            );
        }
    }


    [REnum]
    [REnumFieldEmpty("Ok")]
    [REnumField(typeof(HandleError))]
    public partial struct HandleResult { }

    public readonly struct HandleError
    {
        public readonly string Message;

        public HandleError(string message)
        {
            Message = message;
        }
    }
}
