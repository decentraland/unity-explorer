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
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearableDTO[], DCL.AvatarRendering.Wearables.Components.GetWearableDTOByParamIntention>;

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
            public EntityReference MaleReference;
            public EntityReference FemaleReference;
            public EntityReference BaseWearablesReference;

            public bool FinishedState;
        }

        private readonly DefaultWearableRequest defaultWearableRequest;

        public InstantiateRandomAvatarsSystem(World world) : base(world)
        {
            TOTAL_AVATARS_TO_INSTANTIATE = 100;
            defaultWearableRequest = new DefaultWearableRequest();
        }

        public override void Initialize()
        {
            base.Initialize();

            string[] defaultMaleWearables = new List<string> { WearablesLiterals.BodyShapes.MALE }
                                           .Concat(WearablesLiterals.DefaultWearables.GetDefaultWearablesForBodyShape(WearablesLiterals.BodyShapes.MALE))
                                           .ToArray();

            string[] defaultFemaleWearables = new List<string> { WearablesLiterals.BodyShapes.FEMALE }
                                             .Concat(WearablesLiterals.DefaultWearables.GetDefaultWearablesForBodyShape(WearablesLiterals.BodyShapes.FEMALE))
                                             .ToArray();

            defaultWearableRequest.MaleReference = World.Reference(World.Create(new GetWearablesByPointersIntention
            {
                Pointers = defaultMaleWearables.ToArray(),
                BodyShape = WearablesLiterals.BodyShapes.MALE,
            }, PartitionComponent.TOP_PRIORITY));

            defaultWearableRequest.FemaleReference = World.Reference(World.Create(new GetWearablesByPointersIntention
            {
                Pointers = defaultFemaleWearables.ToArray(),
                BodyShape = WearablesLiterals.BodyShapes.FEMALE,
            }, PartitionComponent.TOP_PRIORITY));

            defaultWearableRequest.BaseWearablesReference = World.Reference(World.Create(new GetWearableByParamIntention
            {
                Params = new[] { ("collectionType", "base-wearable"), ("pageSize", "300") },
                UserID = "DummyUser",
            }, PartitionComponent.TOP_PRIORITY));
        }

        protected override void Update(float t)
        {
            if (defaultWearableRequest.FinishedState)
                return;

            Debug.Log("A " + World.Has<Wearable[]>(defaultWearableRequest.MaleReference));
            Debug.Log("B " + World.Has<Wearable[]>(defaultWearableRequest.FemaleReference));
            Debug.Log("C " + World.Has<Wearable[]>(defaultWearableRequest.BaseWearablesReference));

            if (!World.Has<Wearable[]>(defaultWearableRequest.MaleReference) ||
                !World.Has<Wearable[]>(defaultWearableRequest.FemaleReference) ||
                !World.Has<Wearable[]>(defaultWearableRequest.BaseWearablesReference))
                return;

            GenerateRandomAvatars(World.Get<Wearable[]>(defaultWearableRequest.BaseWearablesReference));
            defaultWearableRequest.FinishedState = true;
        }

        private void GenerateRandomAvatars(Wearable[] defaultWearables)
        {
            var male = new AvatarRandomizerHelper(WearablesLiterals.BodyShapes.MALE);
            var female = new AvatarRandomizerHelper(WearablesLiterals.BodyShapes.FEMALE);

            foreach (Wearable wearable in defaultWearables)
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
                transform.localPosition = new Vector3(Random.Range(0, 20), 0, Random.Range(0, 20));
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

        public void AddWearable(Wearable wearable)
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
