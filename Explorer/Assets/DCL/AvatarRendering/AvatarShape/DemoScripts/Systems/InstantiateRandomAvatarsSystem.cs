using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.CharacterCamera;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using PointerPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;
using Random = UnityEngine.Random;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))] // Updating before AvatarSystem allows it to react as soon as possible
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        private const int MAX_AVATAR_NUMBER = 1000;

        private readonly IRealmData realmData;
        private readonly QueryDescription avatarsQuery;

        private readonly DebugWidgetVisibilityBinding debugVisibilityBinding;
        private readonly ElementBinding<ulong> totalAvatarsInstantiated;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity defaultWearableState;

        private AvatarRandomizer[] randomizers;
        private bool randomizerInitialized;

        internal InstantiateRandomAvatarsSystem(World world, IDebugContainerBuilder debugBuilder, IRealmData realmData, QueryDescription avatarsQuery) : base(world)
        {
            this.realmData = realmData;
            this.avatarsQuery = avatarsQuery;

            debugBuilder.AddWidget("Avatar Debug")
                        .SetVisibilityBinding(debugVisibilityBinding = new DebugWidgetVisibilityBinding(false))
                        .AddIntFieldWithConfirmation(10, "Instantiate", AddRandomAvatar)
                        .AddControl(new DebugConstLabelDef("Total Avatars"), new DebugLongMarkerDef(totalAvatarsInstantiated = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat))
                        .AddSingleButton("Destroy All Avatars", DestroyAllAvatars)
                        .AddSingleButton("Destroy Random Amount of Avatars", DestroyRandomAmountOfAvatars)
                        .AddSingleButton("Randomize Wearables of Avatars", RandomizeWearablesOfAvatars);
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            defaultWearableState = World.CacheDefaultWearablesState();
        }

        private void SetViewActivity()
        {
            debugVisibilityBinding.SetVisible(realmData.Configured && defaultWearableState.GetDefaultWearablesState(World).ResolvedState == DefaultWearablesComponent.State.Success);
        }

        private void RandomizeWearablesOfAvatars()
        {
            World.Query(in avatarsQuery,
                (ref PBAvatarShape pbAvatarShape) =>
                {
                    AvatarRandomizer currentRandomizer = randomizers[Random.Range(0, randomizers.Length)];
                    pbAvatarShape.Wearables.Clear();

                    foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                        pbAvatarShape.Wearables.Add(randomAvatarWearable);

                    pbAvatarShape.BodyShape = currentRandomizer.BodyShape;
                    pbAvatarShape.IsDirty = true;
                });
        }

        private void DestroyRandomAmountOfAvatars()
        {
            World.Query(in avatarsQuery,
                (in Entity entity) =>
                {
                    if (Random.Range(0, 3) == 0)
                    {
                        World.Add(entity, new DeleteEntityIntention());
                        totalAvatarsInstantiated.Value--;
                    }
                });
        }

        private void DestroyAllAvatars()
        {
            // Input events are processed before Update
            World.Add(in avatarsQuery, new DeleteEntityIntention());
            totalAvatarsInstantiated.Value = 0;
        }

        private void AddRandomAvatar(int number)
        {
            int avatarsToInstantiate = Mathf.Clamp(number, 0, MAX_AVATAR_NUMBER - (int)totalAvatarsInstantiated.Value);
            totalAvatarsInstantiated.Value += (uint)avatarsToInstantiate;

            var randomAvatarRequest = new RandomAvatarRequest
            {
                RandomAvatarsToInstantiate = avatarsToInstantiate,
                BaseWearablesPromise = ParamPromise.Create(World,
                    new GetWearableByParamIntention(new[] { ("collectionType", "base-wearable"), ("pageSize", "50") }, "DummyUser", new List<IWearable>()),
                    PartitionComponent.TOP_PRIORITY),
            };

            World.Create(randomAvatarRequest);
        }

        protected override void Update(float t)
        {
            SetViewActivity();
            FinalizeRandomAvatarInstantiationQuery(World, in camera.GetCameraComponent(World));
        }

        [Query]
        private void FinalizeRandomAvatarInstantiation(in Entity entity, [Data] in CameraComponent cameraComponent, ref RandomAvatarRequest randomAvatarRequest)
        {
            if (randomAvatarRequest.BaseWearablesPromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> baseWearables))
            {
                if (baseWearables.Succeeded)
                {
                    GenerateRandomizers(baseWearables);
                    GenerateRandomAvatars(randomAvatarRequest.RandomAvatarsToInstantiate, cameraComponent.Camera.transform.position);
                }
                else
                    ReportHub.LogError(GetReportCategory(), "Base wearables could't be loaded!");

                World.Destroy(entity);
            }
        }

        private void GenerateRandomizers(StreamableLoadingResult<IWearable[]> baseWearables)
        {
            if (randomizerInitialized)
                return;

            var male = new AvatarRandomizer(BodyShape.MALE);
            var female = new AvatarRandomizer(BodyShape.FEMALE);

            foreach (IWearable wearable in baseWearables.Asset)
            {
                male.AddWearable(wearable);
                female.AddWearable(wearable);
            }

            randomizers = new[] { male, female };
            randomizerInitialized = true;
        }

        private void GenerateRandomAvatars(int randomAvatarsToInstantiate, Vector3 cameraPosition)
        {
            float startXPosition = cameraPosition.x;
            float startZPosition = cameraPosition.z;

            //hacky spawn size
            var density = 2.0f;
            float spawnArea = (float)Math.Sqrt(randomAvatarsToInstantiate) * density;

            for (var i = 0; i < randomAvatarsToInstantiate; i++)
            {
                AvatarRandomizer currentRandomizer = randomizers[Random.Range(0, randomizers.Length)];

                float halfSpawnArea = spawnArea / 2;
                float randomX = Random.Range(-halfSpawnArea, halfSpawnArea);
                float randomZ = Random.Range(-halfSpawnArea, halfSpawnArea);

                var wearables = new List<string>();

                foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                    wearables.Add(randomAvatarWearable);

                // Create a transform, normally it will be created either by JS Scene or by Comms
                Transform transform = new GameObject($"RANDOM_AVATAR_{i}").transform;

                var pos = new Vector3(startXPosition + randomX, 500, startZPosition + randomZ);
                const float DISTANCE = 1000.0f;

                if (Physics.Raycast(pos, Vector3.down, out RaycastHit hitInfo, DISTANCE))
                    transform.localPosition = hitInfo.point;
                else
                    transform.localPosition = new Vector3(pos.x, 0.0f, pos.z);

                var transformComp = new TransformComponent(transform);

                var avatarShape = new PBAvatarShape
                {
                    BodyShape = currentRandomizer.BodyShape,
                    Wearables = { wearables },
                    SkinColor = WearablesConstants.DefaultColors.GetRandomSkinColor3(),
                    HairColor = WearablesConstants.DefaultColors.GetRandomHairColor3(),
                };

                World.Create(avatarShape, transformComp);
            }
        }
    }
}
