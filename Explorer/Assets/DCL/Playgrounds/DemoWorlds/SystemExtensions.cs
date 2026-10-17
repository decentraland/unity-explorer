using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;

namespace DCL.DemoWorlds
{
    public static class SystemExtensions
    {
        public static LoadSystemBase<TAsset, TIntention> InitializeAndReturnSelf<TAsset, TIntention>(
            this LoadSystemBase<TAsset, TIntention> system
        ) where TIntention: struct, ILoadingIntention, IEquatable<TIntention>
        {
            system.Initialize();
            return system;
        }
    }
}
