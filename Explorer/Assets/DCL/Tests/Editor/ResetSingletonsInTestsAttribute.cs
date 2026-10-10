using CodeLess.Singletons;
using DCL.Tests.Editor;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: ResetSingletonsInTests]

namespace DCL.Tests.Editor
{
    public class ResetSingletonsInTestsAttribute : TestActionAttribute
    {
        public override void AfterTest(ITest test)
        {
            SingletonRegistry.Reset();
        }
    }
}
