namespace DCL.RuntimeDeepLink
{
    public enum DeepLinkHandleResult
    {
        Consumed,
        NoMatches,

        /// <summary>
        ///     A signin deep link arrived while no login flow was waiting for it. It is left untouched so the
        ///     instance actually logging in can claim it, rather than a concurrent idle Explorer instance
        ///     consuming and deleting the shared bridge file first. Kept until claimed (bounded by a timeout).
        /// </summary>
        Deferred,
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
