using CodeLess.Singletons;
using DCL.Tests.PlayMode;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: ResetSingletonsInTests]

namespace DCL.Tests.PlayMode
{
    public class ResetSingletonsInTestsAttribute : TestActionAttribute
    {
        public override void AfterTest(ITest test)
        {
            SingletonRegistry.Reset();
        }
    }
}
