using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PointerPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    //TODO: Temporary class to load default wearables and create random avatars from it.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        private readonly int TOTAL_AVATARS_TO_INSTANTIATE;

        public class DefaultWearableRequest
        {
            public PointerPromise MalePromise;
            public PointerPromise FemalePromise;
            public ParamPromise BaseWearablesPromise;

            public bool FinishedState;
        }

        private readonly DefaultWearableRequest defaultWearableRequest;

        public InstantiateRandomAvatarsSystem(World world) : base(world)
        {
            TOTAL_AVATARS_TO_INSTANTIATE = RandomAvatarNumberHolder.instance.amoutOfRandomAvatars;
            defaultWearableRequest = new DefaultWearableRequest();
        }

        public override void Initialize()
        {
            base.Initialize();

            var defaultMaleWearables = new List<string> { WearablesLiterals.BodyShape.MALE }
                                      .Concat(WearablesLiterals.DefaultWearables.GetDefaultWearablesForBodyShape(WearablesLiterals.BodyShape.MALE))
                                      .ToList();

            var defaultFemaleWearables = new List<string> { WearablesLiterals.BodyShape.FEMALE }
                                        .Concat(WearablesLiterals.DefaultWearables.GetDefaultWearablesForBodyShape(WearablesLiterals.BodyShape.FEMALE))
                                        .ToList();

            defaultWearableRequest.MalePromise = PointerPromise.Create(World,
                new GetWearablesByPointersIntention(defaultMaleWearables, new IWearable[defaultMaleWearables.Count], WearablesLiterals.BodyShape.MALE),
                PartitionComponent.TOP_PRIORITY);

            defaultWearableRequest.FemalePromise = PointerPromise.Create(World,
                new GetWearablesByPointersIntention(defaultFemaleWearables, new IWearable[defaultFemaleWearables.Count], WearablesLiterals.BodyShape.FEMALE),
                PartitionComponent.TOP_PRIORITY);

            defaultWearableRequest.BaseWearablesPromise = ParamPromise.Create(World,
                new GetWearableByParamIntention(new[] { ("collectionType", "base-wearable"), ("pageSize", "300") }, "DummyUser", new List<IWearable>()),
                PartitionComponent.TOP_PRIORITY);
        }

        protected override void Update(float t)
        {
            if (defaultWearableRequest.FinishedState)
                return;

            if (defaultWearableRequest.MalePromise.TryGetResult(World, out StreamableLoadingResult<IWearable[]> maleResult) &&
                defaultWearableRequest.FemalePromise.TryGetResult(World, out StreamableLoadingResult<IWearable[]> femaleResult) &&
                defaultWearableRequest.BaseWearablesPromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> baseWearables))
            {
                if (baseWearables.Succeeded)
                    GenerateRandomAvatars(baseWearables.Asset);
                else
                    ReportHub.LogError(GetReportCategory(), "Base wearables could't be loaded!");

                defaultWearableRequest.MalePromise.Consume(World);
                defaultWearableRequest.FemalePromise.Consume(World);

                defaultWearableRequest.FinishedState = true;
            }
        }

        private void GenerateRandomAvatars(IWearable[] defaultWearables)
        {
            var male = new AvatarRandomizerHelper(WearablesLiterals.BodyShape.MALE);
            var female = new AvatarRandomizerHelper(WearablesLiterals.BodyShape.FEMALE);

            foreach (IWearable wearable in defaultWearables)
            {
                male.AddWearable(wearable);
                female.AddWearable(wearable);
            }

            for (var i = 0; i < TOTAL_AVATARS_TO_INSTANTIATE; i++)
            {
                AvatarRandomizerHelper currentRandomizer = i % 2 == 0 ? male : female;

                var avatarShape = new PBAvatarShape
                {
                    BodyShape = currentRandomizer.BodyShape,
                };

                foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                    avatarShape.Wearables.Add(randomAvatarWearable);

                // Create a transform, normally it will be created either by JS Scene or by Comms
                Transform transform = new GameObject($"RANDOM_AVATAR_{i}").transform;
                transform.localPosition = new Vector3(Random.Range(-208, -208 + 20), 0, Random.Range(-96, -96 + 20));
                var transformComp = new TransformComponent(transform);

                World.Create(avatarShape, transformComp);
            }
        }
    }

    public class AvatarRandomizerHelper
    {
        public string BodyShape;
        public List<string> upper_body;
        public List<string> lower_body;
        public List<string> feet;
        public List<string> hair;
        public List<string> mouth;
        public List<string> eyes;
        public List<string> eyebros;

        public AvatarRandomizerHelper(string bodyShape)
        {
            BodyShape = bodyShape;
            upper_body = new List<string>();
            lower_body = new List<string>();
            feet = new List<string>();
            hair = new List<string>();
            mouth = new List<string>();
            eyes = new List<string>();
            eyebros = new List<string>();
        }

        public string[] GetRandomAvatarWearables()
        {
            return new[]
            {
                upper_body[Random.Range(0, upper_body.Count)],
                lower_body[Random.Range(0, lower_body.Count)],
                feet[Random.Range(0, feet.Count)],
                hair[Random.Range(0, hair.Count)],

                //TODO: We still dont have the default asset bundles for this ones
                //We should add them before using them
                //mouth[Random.Range(0, mouth.Count)].metadata.id,
                //eyes[Random.Range(0, eyes.Count)].metadata.id,
                //eyebros[Random.Range(0, eyebros.Count)].metadata.id,
            };
        }

        public void AddWearable(IWearable wearable)
        {
            if (!wearable.IsCompatibleWithBodyShape(BodyShape))
                return;

            switch (wearable.GetCategory())
            {
                case WearablesLiterals.Categories.UPPER_BODY:
                    upper_body.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.LOWER_BODY:
                    lower_body.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.FEET:
                    feet.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.HAIR:
                    hair.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.MOUTH:
                    mouth.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.EYES:
                    eyes.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.EYEBROWS:
                    eyebros.Add(wearable.GetUrn());
                    break;
            }
        }
    }
}
