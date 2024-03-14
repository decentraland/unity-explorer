using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Movement;
using DCL.Optimization.Pools;
using ECS;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearablesResponse, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;
using Random = UnityEngine.Random;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.AvatarRendering.DemoScripts.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))] // Updating before AvatarSystem allows it to react as soon as possible
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        private const int MAX_AVATAR_NUMBER = 1000;

        private readonly IRealmData realmData;
        private readonly IComponentPool<Transform> transformPool;

        private readonly DebugWidgetVisibilityBinding debugVisibilityBinding;
        private readonly ElementBinding<ulong> totalAvatarsInstantiated;

        private readonly QueryDescription avatarsQuery;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity defaultWearableState;

        private AvatarRandomizer[] randomizers;
        private bool randomizerInitialized;
        private SingleInstanceEntity settings;
        private int avatarIndex;

        internal InstantiateRandomAvatarsSystem(World world, IDebugContainerBuilder debugBuilder, IRealmData realmData, QueryDescription avatarsQuery, IComponentPool<Transform> componentPools) : base(world)
        {
            this.realmData = realmData;
            this.avatarsQuery = avatarsQuery;
            transformPool = componentPools;

            debugBuilder.AddWidget("Avatar Debug")
                        .SetVisibilityBinding(debugVisibilityBinding = new DebugWidgetVisibilityBinding(false))
                        .AddIntFieldWithConfirmation(10, "Instantiate", AddRandomAvatar)
                        .AddSingleButton("Instantiate Self Replica", AddRandomSelfReplicaAvatar)
                        .AddControl(new DebugConstLabelDef("Total Avatars"), new DebugLongMarkerDef(totalAvatarsInstantiated = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat))
                        .AddSingleButton("Destroy All Avatars", DestroyAllAvatars)
                        .AddSingleButton("Destroy Random Amount of Avatars", DestroyRandomAmountOfAvatars)
                        .AddSingleButton("Randomize Wearables of Avatars", RandomizeWearablesOfAvatars);
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            defaultWearableState = World.CacheDefaultWearablesState();
            settings = World.CacheCharacterSettings();
        }

        private void SetDebugViewActivity()
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
                entity =>
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

        private void AddRandomSelfReplicaAvatar() =>
            AddRandomAvatar(1, true);

        private void AddRandomAvatar(int number) =>
            AddRandomAvatar(number, false);

        private void AddRandomAvatar(int number, bool isSelfReplica)
        {
            int avatarsToInstantiate = Mathf.Clamp(number, 0, MAX_AVATAR_NUMBER - (int)totalAvatarsInstantiated.Value);
            totalAvatarsInstantiated.Value += (uint)avatarsToInstantiate;
            var totalAmount = 0;

            var randomAvatarRequest = new RandomAvatarRequest
            {
                RandomAvatarsToInstantiate = avatarsToInstantiate,
                BaseWearablesPromise = ParamPromise.Create(World,
                    new GetWearableByParamIntention(new[]
                    {
                        ("collectionType", "base-wearable"), ("pageSize", "50"),
                    }, "DummyUser", new List<IWearable>(), totalAmount),
                    PartitionComponent.TOP_PRIORITY),
                IsSelfReplica = isSelfReplica,
            };

            World.Create(randomAvatarRequest);
        }

        protected override void Update(float t)
        {
            SetDebugViewActivity();
            FinalizeRandomAvatarInstantiationQuery(World, in camera.GetCameraComponent(World), in settings.GetCharacterSettings(World));
        }

        [Query]
        private void FinalizeRandomAvatarInstantiation(
            in Entity entity,
            [Data] in CameraComponent cameraComponent,
            [Data] in ICharacterControllerSettings characterControllerSettings,
            ref RandomAvatarRequest randomAvatarRequest)
        {
            if (randomAvatarRequest.BaseWearablesPromise.TryConsume(World, out StreamableLoadingResult<WearablesResponse> baseWearables))
            {
                if (baseWearables.Succeeded)
                {
                    GenerateRandomizers(baseWearables);

                    if (randomAvatarRequest.IsSelfReplica)
                        GenerateSelfReplicaAvatar(cameraComponent.Camera.transform.position, characterControllerSettings);
                    else
                        GenerateRandomAvatars(randomAvatarRequest.RandomAvatarsToInstantiate, cameraComponent.Camera.transform.position, characterControllerSettings);
                }
                else
                    ReportHub.LogError(GetReportCategory(), "Base wearables couldn't be loaded!");

                World.Destroy(entity);
            }
        }

        private void GenerateRandomizers(StreamableLoadingResult<WearablesResponse> baseWearables)
        {
            if (randomizerInitialized)
                return;

            var male = new AvatarRandomizer(BodyShape.MALE);
            var female = new AvatarRandomizer(BodyShape.FEMALE);

            foreach (IWearable wearable in baseWearables.Asset.Wearables)
            {
                male.AddWearable(wearable);
                female.AddWearable(wearable);
            }

            randomizers = new[] { male, female };
            randomizerInitialized = true;
        }

        private void GenerateSelfReplicaAvatar(Vector3 cameraPosition, in ICharacterControllerSettings characterControllerSettings)
        {
            float startXPosition = cameraPosition.x;
            float startZPosition = cameraPosition.z;

            AvatarRandomizer currentRandomizer = randomizers[Random.Range(0, randomizers.Length)];

            var wearables = new List<string>();

            foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                wearables.Add(randomAvatarWearable);

            // Create a transform, normally it will be created either by JS Scene or by Comms
            var transformComp = new CharacterTransform(transformPool.Get());

            transformComp.Transform.position = StartPosition(0, startXPosition, startZPosition);
            transformComp.Transform.name = $"RANDOM_AVATAR_{avatarIndex}";

            CharacterController characterController = transformComp.Transform.gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.4f;
            characterController.height = 2;
            characterController.center = Vector3.up;
            characterController.slopeLimit = 50f;
            characterController.gameObject.layer = PhysicsLayers.CHARACTER_LAYER;

            TrailRenderer trail = transformComp.Transform.gameObject.AddComponent<TrailRenderer>();
            trail.time = 1.0f; // The time in seconds that the trail will fade out over
            trail.startWidth = 0.07f; // The starting width of the trail
            trail.endWidth = 0.07f; // The end

            trail.material = new Material(Shader.Find("Unlit/Color"))
            {
                color = Color.yellow,
            };

            var avatarShape = new PBAvatarShape
            {
                Id = $"User{avatarIndex}",
                Name = $"User{avatarIndex}",
                BodyShape = currentRandomizer.BodyShape,
                Wearables = { wearables },
                SkinColor = WearablesConstants.DefaultColors.GetRandomSkinColor3(),
                HairColor = WearablesConstants.DefaultColors.GetRandomHairColor3(),
            };

            World.Create(avatarShape,
                transformComp,
                new CharacterAnimationComponent(),
                new RemotePlayerMovementComponent(RemotePlayerMovementComponent.TEST_ID),
                new InterpolationComponent(),
                new ExtrapolationComponent(),
                characterControllerSettings
            );
        }

        private void GenerateRandomAvatars(int randomAvatarsToInstantiate, Vector3 cameraPosition, ICharacterControllerSettings characterControllerSettings)
        {
            float startXPosition = cameraPosition.x;
            float startZPosition = cameraPosition.z;

            //hacky spawn size
            var density = 2.0f;
            float spawnArea = (float)Math.Sqrt(randomAvatarsToInstantiate) * density;

            for (var i = 0; i < randomAvatarsToInstantiate; i++)
            {
                AvatarRandomizer currentRandomizer = randomizers[Random.Range(0, randomizers.Length)];
                avatarIndex++;
                var wearables = new List<string>();

                foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                    wearables.Add(randomAvatarWearable);

                // Create a transform, normally it will be created either by JS Scene or by Comms
                var transformComp =
                    new CharacterTransform(transformPool.Get());

                transformComp.Transform.position = StartPosition(spawnArea, startXPosition, startZPosition);
                transformComp.Transform.name = $"RANDOM_AVATAR_{avatarIndex}";

                CharacterController characterController = transformComp.Transform.gameObject.AddComponent<CharacterController>();
                characterController.radius = 0.4f;
                characterController.height = 2;
                characterController.center = Vector3.up;
                characterController.slopeLimit = 50f;
                characterController.gameObject.layer = PhysicsLayers.CHARACTER_LAYER;

                var avatarShape = new PBAvatarShape
                {
                    Id = $"User{avatarIndex}",
                    Name = $"User{avatarIndex}",
                    BodyShape = currentRandomizer.BodyShape,
                    Wearables = { wearables },
                    SkinColor = WearablesConstants.DefaultColors.GetRandomSkinColor3(),
                    HairColor = WearablesConstants.DefaultColors.GetRandomHairColor3(),
                };

                World.Create(avatarShape,
                    transformComp,
                    characterController,
                    new CharacterRigidTransform(),
                    new CharacterAnimationComponent(),
                    new CharacterPlatformComponent(),
                    new StunComponent(),
                    new FeetIKComponent(),
                    new HandsIKComponent(),
                    new HeadIKComponent(),
                    new JumpInputComponent(),
                    new MovementInputComponent(),
                    characterControllerSettings
                );
            }
        }

        private static Vector3 StartPosition(float spawnArea, float startXPosition, float startZPosition)
        {
            float halfSpawnArea = spawnArea / 2;
            float randomX = Random.Range(-halfSpawnArea, halfSpawnArea);
            float randomZ = Random.Range(-halfSpawnArea, halfSpawnArea);
            var pos = new Vector3(startXPosition + randomX, 500, startZPosition + randomZ);

            const float RAYCAST_DIST = 1000.0f;

            return Physics.Raycast(pos, Vector3.down, out RaycastHit hitInfo, RAYCAST_DIST)
                ? hitInfo.point
                : new Vector3(pos.x, 0.0f, pos.z);
        }
    }
}
