using DCL.Diagnostics;
using REnum;

namespace DCL.RuntimeDeepLink
{
    public interface IDeepLinkHandle
    {
        string Name { get; }

        /// <summary>
        /// Implementations of the method must be exception free.
        /// </summary>
        HandleResult HandleDeepLink(string deeplink);

        class Null : IDeepLinkHandle
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }

            public string Name => "Null Implementation";

            public HandleResult HandleDeepLink(string deeplink)
            {
                ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"{Name} successfully received deeplink: {deeplink}");
                return HandleResult.Ok();
            }
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
