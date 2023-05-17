using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace ECS.ComponentsPooling.Tests
{
    [TestFixture]
    public class UnityComponentPoolShould
    {
        private UnityComponentPool<Transform> unityComponentPool;
        private Func<Transform> onCreate;
        private Action<Transform> onRelease;
        private Action<Transform> onGet;

        [SetUp]
        public void SetUp()
        {
            onCreate = Substitute.For<Func<Transform>>();
            onRelease = Substitute.For<Action<Transform>>();
            onGet = Substitute.For<Action<Transform>>();
            unityComponentPool = new UnityComponentPool<Transform>(onCreate, onGet, onRelease);
        }

        [Test]
        public void ExecuteCreation()
        {
            for (var i = 0; i < 10; i++)
                unityComponentPool.Get();

            onCreate.Received(10).Invoke();
        }

        [TearDown]
        public void TearDown()
        {
            unityComponentPool.Clear();
        }
    }
}
