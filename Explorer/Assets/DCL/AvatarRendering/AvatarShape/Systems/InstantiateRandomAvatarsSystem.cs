using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LoadWearableSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        private readonly int TOTAL_AVATARS_TO_INSTANTIATE;

        public InstantiateRandomAvatarsSystem(World world) : base(world)
        {
            TOTAL_AVATARS_TO_INSTANTIATE = 100;
        }

        public override void Initialize()
        {
            base.Initialize();
            (string, string)[] urlParams = { ("collectionType", "base-wearable"), ("pageSize", "300") };

            //TODO: Probably once again we need a prepare system to resolver the url
            var promise = AssetPromise<WearableDTO[], GetWearableByParamIntention>.Create(World,
                new GetWearableByParamIntention
                {
                    CommonArguments = new CommonLoadingArguments("DummyUser"),
                    Params = urlParams,
                    UserID = "DummyUser",
                },
                new PartitionComponent());

            var randomAvatarConstructorComponent = new RandomAvatarConstructorComponent();
            randomAvatarConstructorComponent.WearableRequestPromise = promise;
            World.Create(randomAvatarConstructorComponent);
        }

        protected override void Update(float t)
        {
            CreateRandomAvatarsQuery(World);
        }

        [Query]
        private void CreateRandomAvatars(ref RandomAvatarConstructorComponent randomAvatarConstructorComponent)
        {
            if (!randomAvatarConstructorComponent.Done &&
                randomAvatarConstructorComponent.WearableRequestPromise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> result))
            {
                if (!result.Succeeded)
                    ReportHub.LogError(GetReportCategory(), "Base wearables could not be fetched");
                else
                    GenerateRandomAvatars(result);

                randomAvatarConstructorComponent.Done = true;
            }
        }

        private void GenerateRandomAvatars(StreamableLoadingResult<WearableDTO[]> result)
        {
            var body_shape = new List<WearableDTO>();
            var upper_body = new List<WearableDTO>();
            var lower_body = new List<WearableDTO>();
            var feet = new List<WearableDTO>();
            var hair = new List<WearableDTO>();

            foreach (WearableDTO wearableDto in result.Asset)
            {
                switch (wearableDto.metadata.data.category)
                {
                    case "body_shape":
                        body_shape.Add(wearableDto);
                        break;
                    case "upper_body":
                        upper_body.Add(wearableDto);
                        break;
                    case "lower_body":
                        lower_body.Add(wearableDto);
                        break;
                    case "feet":
                        feet.Add(wearableDto);
                        break;
                    case "hair":
                        hair.Add(wearableDto);
                        break;
                }
            }

            for (var i = 0; i < TOTAL_AVATARS_TO_INSTANTIATE; i++)
            {
                var avatarShape = new PBAvatarShape
                {
                    BodyShape = body_shape[Random.Range(0, body_shape.Count)].metadata.id,
                    Wearables =
                    {
                        upper_body[Random.Range(0, upper_body.Count)].metadata.id,
                        lower_body[Random.Range(0, lower_body.Count)].metadata.id,
                        feet[Random.Range(0, feet.Count)].metadata.id,
                        hair[Random.Range(0, hair.Count)].metadata.id,
                    },
                };

                World.Create(avatarShape);
            }
        }
    }
}
