using Global.AppArgs;
using Utility.Types;

namespace DCL.RuntimeDeepLink
{
    public interface IDeepLinkHandle
    {
        public string Name { get; }

        /// <summary>
        /// Implementations of the method must be exception free.
        /// </summary>
        public Result HandleDeepLink(IAppArgs appArgs);

        class Null : IDeepLinkHandle
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }

            public string Name => "Null Implementation";

            public Result HandleDeepLink(IAppArgs appArgs) =>
                Result.SuccessResult();
        }
    }
}
