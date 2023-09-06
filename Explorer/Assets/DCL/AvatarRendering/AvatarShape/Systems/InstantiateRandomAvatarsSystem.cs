using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearableDTO[], DCL.AvatarRendering.Wearables.Components.GetWearableByParamIntention>;


namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareWearableSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        public struct GenerateRandomAvatarComponent { }

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
            var promise = Promise.Create(World,
                new GetWearableByParamIntention
                {
                    CommonArguments = new CommonLoadingArguments("DummyUser"),
                    Params = urlParams,
                    UserID = "DummyUser",
                },
                new PartitionComponent());

            World.Create(new GenerateRandomAvatarComponent(), promise);
        }

        protected override void Update(float t)
        {
            CreateRandomAvatarsQuery(World);
        }

        [Query]
        [All(typeof(GenerateRandomAvatarComponent))]
        private void CreateRandomAvatars(in Entity entity, ref Promise wearablePromise)
        {
            if (wearablePromise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> result))
            {
                if (!result.Succeeded)
                    ReportHub.LogError(GetReportCategory(), "Base wearables could not be fetched");
                else
                    GenerateRandomAvatars(result);

                World.Destroy(entity);
            }
        }

        private void GenerateRandomAvatars(StreamableLoadingResult<WearableDTO[]> result)
        {
            var body_shape = new List<WearableDTO>();
            var upper_body = new List<WearableDTO>();
            var lower_body = new List<WearableDTO>();
            var feet = new List<WearableDTO>();
            var hair = new List<WearableDTO>();
            var mouth = new List<WearableDTO>();
            var eyes = new List<WearableDTO>();
            var eyebros = new List<WearableDTO>();

            foreach (WearableDTO wearableDto in result.Asset)
            {
                switch (wearableDto.metadata.data.category)
                {
                    case WearablesLiterals.Categories.BODY_SHAPE:
                        body_shape.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.UPPER_BODY:
                        upper_body.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.LOWER_BODY:
                        lower_body.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.FEET:
                        feet.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.HAIR:
                        hair.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.MOUTH:
                        mouth.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.EYES:
                        eyes.Add(wearableDto);
                        break;
                    case WearablesLiterals.Categories.EYEBROWS:
                        eyebros.Add(wearableDto);
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

                        //TODO: We still dont have the default asset bundles for this ones
                        //We should add them before using them
                        //mouth[Random.Range(0, mouth.Count)].metadata.id,
                        //eyes[Random.Range(0, eyes.Count)].metadata.id,
                        //eyebros[Random.Range(0, eyebros.Count)].metadata.id,
                    },
                };

                // Create a transform, normally it will be created either by JS Scene or by Comms
                Transform transform = new GameObject($"RANDOM_AVATAR_{i}").transform;
                transform.localPosition = new Vector3(Random.Range(0, 20), 0, Random.Range(0, 20));
                var transformComp = new TransformComponent(transform);

                World.Create(avatarShape, transformComp);
            }
        }
    }
}
