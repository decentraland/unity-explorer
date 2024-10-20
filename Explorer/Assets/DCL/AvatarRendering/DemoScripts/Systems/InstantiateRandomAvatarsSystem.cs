using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.DemoScripts.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
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
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.Profiles;
using ECS;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.PriorityQueue;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearablesResponse, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;
using Random = UnityEngine.Random;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.AvatarRendering.DemoScripts.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))] // Updating before AvatarSystem allows it to react as soon as possible
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        private const int MAX_AVATAR_NUMBER = 1000;

        private static readonly QueryDescription AVATARS_QUERY = new QueryDescription()
            .WithAll<Profile, RandomAvatar, CharacterTransform>().WithNone<PlayerComponent>();

        private readonly IRealmData realmData;
        private readonly IComponentPool<Transform> transformPool;

        private readonly DebugWidgetVisibilityBinding? debugVisibilityBinding;
        private readonly ElementBinding<ulong> totalAvatarsInstantiated;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity defaultWearableState;

        private AvatarRandomizer[] randomizers;
        private SingleInstanceEntity settings;
        private int avatarIndex;

        private bool requestDone;
        private int lastIndexInstantiated;
        private readonly AvatarRandomizerAsset avatarRandomizerAsset;

        private bool networkAvatar;

        internal InstantiateRandomAvatarsSystem(
            World world,
            IDebugContainerBuilder debugBuilder,
            IRealmData realmData,
            IComponentPool<Transform> componentPools,
            AvatarRandomizerAsset avatarRandomizerAsset
        ) : base(world)
        {
            this.realmData = realmData;
            transformPool = componentPools;
            this.avatarRandomizerAsset = avatarRandomizerAsset;
            networkAvatar = true;

            debugBuilder.TryAddWidget("Avatar Debug")
                       ?.SetVisibilityBinding(debugVisibilityBinding = new DebugWidgetVisibilityBinding(false))
                        .AddToggleField("Network avatar", evt => networkAvatar = evt.newValue, true)
                        .AddIntFieldWithConfirmation(30, "Instantiate", AddRandomAvatar)
                        .AddControl(new DebugConstLabelDef("Total Avatars"), new DebugLongMarkerDef(totalAvatarsInstantiated = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat))
                        .AddSingleButton("Destroy All Avatars", DestroyAllAvatars)
                        .AddSingleButton("Destroy Random Amount of Avatars", DestroyRandomAmountOfAvatars)
                        .AddSingleButton("Randomize Wearables of Avatars", RandomizeWearablesOfAvatars);

            debugBuilder.TryAddWidget("Avatar Creator")
                ?.AddStringFieldsWithConfirmation(3, "Instantiate Male", InstantiateMaleAvatar)
                .AddStringFieldsWithConfirmation(3, "Instantiate Female", InstantiateFemaleAvatar);
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            defaultWearableState = World.CacheDefaultWearablesState();
            settings = World.CacheCharacterSettings();
        }

        private void InstantiateMaleAvatar(string[] urn)
        {
            var cameraPosition = camera.GetCameraComponent(World).Camera.transform.position;
            CreateAvatar(settings.GetCharacterSettings(World), cameraPosition.x, cameraPosition.z, urn.Where(s => !string.IsNullOrEmpty(s)).ToList(),
                BodyShape.MALE, lastIndexInstantiated, 1);
            lastIndexInstantiated++;
        }

        private void InstantiateFemaleAvatar(string[] urn)
        {
            var cameraPosition = camera.GetCameraComponent(World).Camera.transform.position;
            CreateAvatar(settings.GetCharacterSettings(World), cameraPosition.x, cameraPosition.z, urn.Where(s => !string.IsNullOrEmpty(s)).ToList(),
                BodyShape.FEMALE, lastIndexInstantiated, 1);
            lastIndexInstantiated++;
        }

        private void SetDebugViewActivity()
        {
            debugVisibilityBinding?.SetVisible(realmData.Configured && defaultWearableState.GetDefaultWearablesState(World).ResolvedState == DefaultWearablesComponent.State.Success);
        }

        private void RandomizeWearablesOfAvatars()
        {
            World.Query(in AVATARS_QUERY,
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
            
            World.Query(in AVATARS_QUERY,
                (Entity entity, ref CharacterTransform transformComponent) =>  
                {
                    Object.Destroy(transformComponent.Transform.gameObject.GetComponent<CharacterController>());
                    if (Random.Range(0, 3) == 0)
                    {
                        World.Add(entity, new DeleteEntityIntention());
                        totalAvatarsInstantiated.Value--;
                    }
                });
        }

        private void DestroyAllAvatars()
        {
            World.Query(in AVATARS_QUERY,
                (Entity entity, ref CharacterTransform transformComponent) =>
                {
                    Object.Destroy(transformComponent.Transform.gameObject.GetComponent<CharacterController>());
                    World.Add(entity, new DeleteEntityIntention());
                });
            
            totalAvatarsInstantiated.Value = 0;
        }

        private void AddRandomAvatar(int number)
        {
            int avatarsToInstantiate = Mathf.Clamp(number, 0, MAX_AVATAR_NUMBER - (int)totalAvatarsInstantiated.Value);
            totalAvatarsInstantiated.Value += (uint)avatarsToInstantiate;

            if (requestDone)
            {
                GenerateRandomAvatars(avatarsToInstantiate, camera.GetCameraComponent(World).Camera.transform.position, settings.GetCharacterSettings(World));
                return;
            }

            var collectionPromises = new List<ParamPromise>
            {
                ParamPromise.Create(World,
                    new GetWearableByParamIntention(new[]
                    {
                        ("collectionType", "base-wearable"), ("pageSize", "282"),
                    }, "DummyUser", new List<IWearable>(), 0),
                    PartitionComponent.TOP_PRIORITY),
            };

            var randomAvatarRequest = new RandomAvatarRequest
            {
                RandomAvatarsToInstantiate = avatarsToInstantiate,
                CollectionPromise = collectionPromises,
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
                if (!assetPromise.TryGetResult(World, out StreamableLoadingResult<WearablesResponse> _))
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
                    ReportHub.LogError(GetReportData(), $"Collection {assetPromise.LoadingIntention.Params[0].Item2} couldn't be loaded!");
            }

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
            
            HashSet<URN> wearablesURN = new HashSet<URN>();
            foreach (string wearable in wearables)
                wearablesURN.Add(new URN(wearable));

            var profile = Profile.Create(
                StringUtils.GenerateRandomString(5),
                StringUtils.GenerateRandomString(5),
                new Avatar(BodyShape.FromStringSafe(bodyShape), wearablesURN, WearablesConstants.DefaultColors.GetRandomEyesColor(), WearablesConstants.DefaultColors.GetRandomHairColor(), WearablesConstants.DefaultColors.GetRandomSkinColor()));


            if (networkAvatar)
            {
                World.Create(profile,
                    transformComp,
                    new CharacterAnimationComponent(),
                    new CharacterEmoteComponent(),
                    new RandomAvatar());
            }
            else
            {
                var characterController = transformComp.Transform.gameObject.AddComponent<CharacterController>();
                characterController.radius = 0.4f;
                characterController.height = 2;
                characterController.center = Vector3.up;
                characterController.slopeLimit = 50f;
                characterController.gameObject.layer = PhysicsLayers.CHARACTER_LAYER;

                World.Create(profile,
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
                    characterControllerSettings,
                    new RandomAvatar()
                );
            }
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
