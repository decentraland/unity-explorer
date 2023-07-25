using Cysharp.Threading.Tasks;
using ECS.Profiling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using NUnit.Framework;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace ECS.StreamableLoading.DeferredLoading.Tests
{
    [TestFixture]
    public class CapFrameTimeBudgetProviderShould
    {
        [UnityTest]
        public IEnumerator SpendBudget()
        {
            ProfilingProvider provider = new ProfilingProvider();

            // Arrange
            var budgetProvider = new FrameTimeCapBudgetProvider(0.5f, provider);

            yield return UniTask.Yield();

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());


            for(int i=0;i<1000;i++)
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }

    }
}
