using DCL.Utility.Types;

namespace DCL.RuntimeDeepLink
{
    public interface IDeepLinkHandle
    {
        public string Name { get; }

        /// <summary>
        /// Implementations of the method must be exception free.
        /// Returns <c>true</c> when the deep link was consumed and its bridge file may be deleted;
        /// <c>false</c> to leave the bridge file in place for a later attempt.
        /// </summary>
        public bool HandleDeepLink(DeepLink deeplink);

        class Null : IDeepLinkHandle
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }

            public string Name => "Null Implementation";

            public bool HandleDeepLink(DeepLink deeplink) =>
                true;
        }
    }
}
