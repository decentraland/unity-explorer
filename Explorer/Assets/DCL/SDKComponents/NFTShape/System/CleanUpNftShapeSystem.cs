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
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.Textures;
using UnityEngine;
using UnityEngine.Networking;

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
            if (forgetPromise)
                nftLoadingComponent.TypePromise.ForgetLoading(World);

            if (nftLoadingComponent.ImagePromise != null)
            {
                // TODO: check if we dont get a reference miss here, since .Value provides a copy of the promise
                var promise = nftLoadingComponent.ImagePromise.Value;
                promise.TryDereference(World);

                if (forgetPromise)
                    promise.ForgetLoading(World);
            }

            if (nftLoadingComponent.VideoPromise != null)
            {
                // TODO: check if we dont get a reference miss here, since .Value provides a copy of the promise
                var promise = nftLoadingComponent.VideoPromise.Value;
                promise.TryDereference(World);

                if (forgetPromise)
                    promise.ForgetLoading(World);
            }
        }

        public void FinalizeComponents(in Query query)
        {
            AbortLoadingOnWorldFinalizationQuery(World);
        }
    }
}
