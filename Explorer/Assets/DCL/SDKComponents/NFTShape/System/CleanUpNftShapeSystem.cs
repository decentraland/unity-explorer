using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Cache;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [ThrottlingEnabled]
    public partial class CleanUpNftShapeSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        internal CleanUpNftShapeSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            AbortLoadingOnEntityDeletionQuery(World);
            AbortLoadingIfComponentDeletedQuery(World);
        }

        [Query]
        [None(typeof(PBNftShape), typeof(DeleteEntityIntention))]
        private void AbortLoadingIfComponentDeleted(ref NFTLoadingComponent nftLoadingComponent)
        {
            AbortLoading(ref nftLoadingComponent, true);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void AbortLoadingOnEntityDeletion(ref NFTLoadingComponent nftLoadingComponent)
        {
            AbortLoading(ref nftLoadingComponent, true);
        }

        [Query]
        private void AbortLoadingOnWorldFinalization(ref NFTLoadingComponent nftLoadingComponent)
        {
            AbortLoading(ref nftLoadingComponent, false);
        }

        private void AbortLoading(ref NFTLoadingComponent nftLoadingComponent, bool forgetPromise)
        {
            nftLoadingComponent.Promise.TryDereference(World);

            if (forgetPromise)
                nftLoadingComponent.Promise.ForgetLoading(World);
        }

        public void FinalizeComponents(in Query query)
        {
            AbortLoadingOnWorldFinalizationQuery(World);
        }
    }
}
