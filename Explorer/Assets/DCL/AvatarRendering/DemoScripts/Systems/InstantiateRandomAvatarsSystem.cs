using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Character.CharacterMotion.Components;
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
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using ECS;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.PriorityQueue;
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
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IComponentPool<Transform> transformPool;

        private readonly DebugWidgetVisibilityBinding debugVisibilityBinding;
        private readonly ElementBinding<ulong> totalAvatarsInstantiated;

        private readonly QueryDescription avatarsQuery;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity defaultWearableState;

        private AvatarRandomizer[] randomizers;
        private SingleInstanceEntity settings;
        private int avatarIndex;

        private bool requestDone;
        private readonly AvatarRandomizerAsset avatarRandomizerAsset;

        internal InstantiateRandomAvatarsSystem(
            World world,
            IDebugContainerBuilder debugBuilder,
            IRealmData realmData,
            IEntityParticipantTable entityParticipantTable,
            QueryDescription avatarsQuery,
            IComponentPool<Transform> componentPools,
            AvatarRandomizerAsset avatarRandomizerAsset
        ) : base(world)
        {
            this.realmData = realmData;
            this.entityParticipantTable = entityParticipantTable;
            this.avatarsQuery = avatarsQuery;
            transformPool = componentPools;
            this.avatarRandomizerAsset = avatarRandomizerAsset;

            debugBuilder.AddWidget("Avatar Debug")
                        .SetVisibilityBinding(debugVisibilityBinding = new DebugWidgetVisibilityBinding(false))
                        .AddIntFieldWithConfirmation(30, "Instantiate", AddRandomAvatar)
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

            if (requestDone)
            {
                GenerateRandomAvatars(avatarsToInstantiate, camera.GetCameraComponent(World).Camera.transform.position, settings.GetCharacterSettings(World));
                return;
            }

            var collectionPromises = new List<ParamPromise>();

            collectionPromises.Add(ParamPromise.Create(World,
                new GetWearableByParamIntention(new[]
                {
                    ("collectionType", "base-wearable"), ("pageSize", "282"),
                }, "DummyUser", new List<IWearable>(), 0),
                PartitionComponent.TOP_PRIORITY));

            var randomAvatarRequest = new RandomAvatarRequest
            {
                RandomAvatarsToInstantiate = avatarsToInstantiate,
                CollectionPromise = collectionPromises,
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
            foreach (ParamPromise assetPromise in randomAvatarRequest.CollectionPromise)
            {
                if (!assetPromise.TryGetResult(World, out StreamableLoadingResult<WearablesResponse> collection))
                    return;
            }

            var male = new AvatarRandomizer(BodyShape.MALE);
            var female = new AvatarRandomizer(BodyShape.FEMALE);

            foreach (ParamPromise assetPromise in randomAvatarRequest.CollectionPromise)
            {
                assetPromise.TryConsume(World, out StreamableLoadingResult<WearablesResponse> baseWearables);

                if (baseWearables.Succeeded)
                    GenerateRandomizers(baseWearables, male, female);
                else
                    ReportHub.LogError(GetReportCategory(), $"Collection {assetPromise.LoadingIntention.Params[0].Item2} couldn't be loaded!");
            }

            if (randomAvatarRequest.IsSelfReplica)
                GenerateSelfReplicaAvatar(cameraComponent.Camera.transform.position, characterControllerSettings);
            else
                GenerateRandomAvatars(randomAvatarRequest.RandomAvatarsToInstantiate, cameraComponent.Camera.transform.position, characterControllerSettings);

            requestDone = true;
            World.Destroy(entity);
        }

        private void GenerateRandomizers(StreamableLoadingResult<WearablesResponse> baseWearables, AvatarRandomizer male, AvatarRandomizer female)
        {
            foreach (IWearable wearable in baseWearables.Asset.Wearables)
            {
                male.AddWearable(wearable);
                female.AddWearable(wearable);
            }

            randomizers = new[] { male, female };
        }

        private void GenerateRandomAvatars(int randomAvatarsToInstantiate, Vector3 cameraPosition, ICharacterControllerSettings characterControllerSettings)
        {
            float startXPosition = cameraPosition.x;
            float startZPosition = cameraPosition.z;

            for (var i = 0; i < avatarRandomizerAsset.Avatars.Count && i < randomAvatarsToInstantiate; i++)
            {
                AvatarRandomizer currentRandomizer = randomizers[Random.Range(0, randomizers.Length)];
                var wearables = new List<string>();

                foreach (string avatarWearable in avatarRandomizerAsset.Avatars[i].pointers)
                    wearables.Add(avatarWearable);

                CreateAvatar(characterControllerSettings, startXPosition, startZPosition, wearables, currentRandomizer.BodyShape, i, randomAvatarsToInstantiate);
            }

            for (int i = avatarRandomizerAsset.Avatars.Count; i < randomAvatarsToInstantiate; i++)
            {
                AvatarRandomizer currentRandomizer = randomizers[Random.Range(0, randomizers.Length)];
                avatarIndex++;
                var wearables = new List<string>();

                foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                    wearables.Add(randomAvatarWearable);

                CreateAvatar(characterControllerSettings, startXPosition, startZPosition, wearables, currentRandomizer.BodyShape, i, randomAvatarsToInstantiate);
            }
        }

        private void CreateAvatar(ICharacterControllerSettings characterControllerSettings, float startXPosition, float startZPosition, List<string> wearables, string bodyShape,
            int avatarIndex, int randomAvatarToInstantiate)
        {
            // Create a transform, normally it will be created either by JS Scene or by Comms
            var transformComp =
                new CharacterTransform(transformPool.Get());

            if (avatarRandomizerAsset.RandomOrder)
            {
                //hacky spawn size
                var density = 2.0f;
                float spawnArea = (float)Math.Sqrt(randomAvatarToInstantiate) * density;
                transformComp.Transform.position = StartRandomPosition(spawnArea, startXPosition, startZPosition);
            }
            else { transformComp.Transform.position = new Vector3(startXPosition + (avatarIndex * 2), 3, startZPosition); }

            transformComp.Transform.name = $"RANDOM_AVATAR_{avatarIndex}";

            CharacterController characterController = transformComp.Transform.gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.4f;
            characterController.height = 2;
            characterController.center = Vector3.up;
            characterController.slopeLimit = 50f;
            characterController.gameObject.layer = PhysicsLayers.CHARACTER_LAYER;

            var avatarShape = new PBAvatarShape
            {
                Id = StringUtils.GenerateRandomString(5),
                Name = StringUtils.GenerateRandomString(5),
                BodyShape = bodyShape,
                Wearables = { wearables },
                SkinColor = WearablesConstants.DefaultColors.GetRandomSkinColor3(),
                HairColor = WearablesConstants.DefaultColors.GetRandomHairColor3(),
                EyeColor = WearablesConstants.DefaultColors.GetRandomEyesColor3()
            };

            World.Create(avatarShape,
                transformComp,
                characterController,
                new CharacterRigidTransform(),
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
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

            transformComp.Transform.position = StartRandomPosition(0, startXPosition, startZPosition);
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

            var entity = World.Create(avatarShape,
                transformComp,
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new RemotePlayerMovementComponent(
                    new ObjectPool<SimplePriorityQueue<NetworkMovementMessage>>(() => new SimplePriorityQueue<NetworkMovementMessage>())
                ),
                new InterpolationComponent(),
                new ExtrapolationComponent(),
                characterControllerSettings
            );

            entityParticipantTable.Register(RemotePlayerMovementComponent.TEST_ID, entity);
        }

        private static Vector3 StartRandomPosition(float spawnArea, float startXPosition, float startZPosition)
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
