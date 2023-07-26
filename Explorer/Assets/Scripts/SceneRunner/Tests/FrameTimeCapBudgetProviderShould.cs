using Cysharp.Threading.Tasks;
using ECS.Profiling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneRunner.Tests
{
    [TestFixture]
    public class FrameTimeCapBudgetProviderShould
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


            for(int i=0;i<10_000;i++)
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }

    }
}
