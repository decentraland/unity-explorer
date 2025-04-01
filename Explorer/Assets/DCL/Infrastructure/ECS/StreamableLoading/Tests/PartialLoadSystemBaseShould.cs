using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;

namespace ECS.StreamableLoading.Tests
{
    public abstract class PartialLoadSystemBaseShould<TSystem, TAsset, TIntention> : UnitySystemTestBase<TSystem>
        where TSystem: PartialDownloadSystemBase<TAsset, TIntention>
        where TIntention: struct, ILoadingIntention, IEquatable<TIntention>
    {
        protected IStreamableCache<TAsset, TIntention> cache;

        protected abstract TSystem CreateSystem();

        [SetUp]
        public void BaseSetUp()
        {
            cache = Substitute.For<IStreamableCache<TAsset, TIntention>>();
            system = CreateSystem();
            system.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
        }
    }
}
