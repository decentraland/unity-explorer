using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
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
    //Temporary class to load default wearables and create random avatars from it.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {

        private readonly int TOTAL_AVATARS_TO_INSTANTIATE;

        //TODO: Used to avoid getting results from another system
        public struct InstantiatingRandomAvatarFlag { }

        public InstantiateRandomAvatarsSystem(World world) : base(world)
        {
            TOTAL_AVATARS_TO_INSTANTIATE = 100;
        }

        public override void Initialize()
        {
            base.Initialize();

            var defaultMaleWearables = new List<string>();
            defaultMaleWearables.Add(WearablesLiterals.BodyShapes.MALE);
            defaultMaleWearables.AddRange(WearablesLiterals.DefaultWearables.GetDefaultWearablesForBodyShape(WearablesLiterals.BodyShapes.MALE));

            //TODO: Securing DefaultWearables
            //We will use the male body shape as reference; but we should do this for male and then for female
            World.Create(new GetWearableByPointersIntention
            {
                Pointers = defaultMaleWearables.ToArray(),
                results = new Wearable[defaultMaleWearables.Count],
                BodyShape = WearablesLiterals.BodyShapes.MALE,
            }, PartitionComponent.TOP_PRIORITY, new InstantiatingRandomAvatarFlag());
        }

        protected override void Update(float t)
        {
            FinalizeRandomAvatarsQuery(World);
        }

        [Query]
        [All(typeof(InstantiatingRandomAvatarFlag))]
        private void FinalizeRandomAvatars(in Entity entity, ref StreamableLoadingResult<Wearable[]> defaultWearablesLoaded)
        {
            if (!defaultWearablesLoaded.Succeeded)
                ReportHub.LogError(GetReportCategory(), "Base wearables could not be fetched, we are in an irrecoverable state");
            else
                GenerateRandomAvatars(defaultWearablesLoaded.Asset);

            World.Destroy(entity);
        }

        private void GenerateRandomAvatars(Wearable[] defaultWearables)
        {
            var body_shape = new List<Wearable>();
            var upper_body = new List<Wearable>();
            var lower_body = new List<Wearable>();
            var feet = new List<Wearable>();
            var hair = new List<Wearable>();
            var mouth = new List<Wearable>();
            var eyes = new List<Wearable>();
            var eyebros = new List<Wearable>();

            foreach (Wearable wearable in defaultWearables)
            {
                switch (wearable.GetCategory())
                {
                    case WearablesLiterals.Categories.BODY_SHAPE:
                        body_shape.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.UPPER_BODY:
                        upper_body.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.LOWER_BODY:
                        lower_body.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.FEET:
                        feet.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.HAIR:
                        hair.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.MOUTH:
                        mouth.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.EYES:
                        eyes.Add(wearable);
                        break;
                    case WearablesLiterals.Categories.EYEBROWS:
                        eyebros.Add(wearable);
                        break;
                }
            }

            for (var i = 0; i < TOTAL_AVATARS_TO_INSTANTIATE; i++)
            {
                var avatarShape = new PBAvatarShape
                {
                    BodyShape = body_shape[Random.Range(0, body_shape.Count)].GetUrn(),
                    Wearables =
                    {
                        upper_body[Random.Range(0, upper_body.Count)].GetUrn(),
                        lower_body[Random.Range(0, lower_body.Count)].GetUrn(),
                        feet[Random.Range(0, feet.Count)].GetUrn(),
                        hair[Random.Range(0, hair.Count)].GetUrn(),

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
