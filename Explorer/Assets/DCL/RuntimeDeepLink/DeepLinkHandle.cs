namespace DCL.RuntimeDeepLink
{
    public enum DeepLinkHandleResult
    {
        Consumed,
        NoMatches,

        /// <summary>
        ///     A signin deep link arrived that this instance must not consume: either no login here is waiting
        ///     for one, or its authRequestId does not match the request this login minted. It is left untouched
        ///     so the instance it was minted for can claim it from the shared bridge file, rather than a
        ///     concurrent or idle Explorer instance consuming and deleting it first. Kept until claimed by the
        ///     matching login (bounded by a timeout).
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
