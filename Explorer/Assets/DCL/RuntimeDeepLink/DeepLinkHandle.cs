using DCL.Utility.Types;
using Global.AppArgs;

namespace DCL.RuntimeDeepLink
{
    public interface IDeepLinkHandle
    {
        public string Name { get; }

        /// <summary>
        /// Implementations of the method must be exception free.
        /// </summary>
        public Result Handle(IAppArgs appArgs);

        class Null : IDeepLinkHandle
        {
            public static readonly Null INSTANCE = new ();

            private Null() { }

            public string Name => "Null Implementation";

            public Result Handle(IAppArgs appArgs) =>
                Result.SuccessResult();
        }
    }
}
