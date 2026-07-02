namespace DCL.RuntimeDeepLink
{
    public enum DeepLinkHandleResult
    {
        Consumed,
        NoMatches,
    }

    public interface IDeepLinkHandle
    {
        DeepLinkHandleResult HandleDeepLink(DeepLink deeplink);

        class Null : IDeepLinkHandle
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }


            public DeepLinkHandleResult HandleDeepLink(DeepLink deeplink) =>
                DeepLinkHandleResult.Consumed;
        }
    }
}
