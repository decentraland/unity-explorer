using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.Multiplayer.Profiles.Entities;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS;
using ECS.Abstract;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(EnableInWorldCameraSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class CameraScreencaptureSystem : BaseUnityLoopSystem
    {
        private readonly ScreenRecorder recorder;
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private readonly Entity playerEntity;
        private readonly SelfProfile selfProfile;
        private readonly CharacterController characterObjectController;

        private readonly RealmData realmData;
        private readonly IPlacesAPIService placesAPIService;

        private readonly List<VisiblePerson> visiblePeople = new (32);
        private readonly ScreenshotHudView hud;

        private bool isInstantiated;

        private SingleInstanceEntity cameraEntity;

        public CameraScreencaptureSystem(World world, ScreenRecorder recorder, DCLInput.InWorldCameraActions inputSchema, ScreenshotHudView hud, SelfProfile selfProfile, RealmData realmData,
            Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject) : base(world)
        {
            this.recorder = recorder;
            this.inputSchema = inputSchema;
            this.hud = hud;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            characterObjectController = characterObject.Controller;
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (recorder.IsCapturing) return;

            if (inputSchema.Screenshot.triggered && World.Has<IsInWorldCamera>(cameraEntity))
            {
                hud.Canvas.enabled = false;

                hud.StartCoroutine(
                    recorder.CaptureScreenshot(
                        hud.AssignScreenshot));

                visiblePeople.Clear();
                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cameraEntity.GetCameraComponent(World).Camera);

                AddSelfProfile(frustumPlanes);
                CollectVisiblePeopleQuery(World, frustumPlanes);
                CollectMetadata().Forget();
            }
        }

        private void AddSelfProfile(Plane[] frustumPlanes)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, characterObjectController.bounds))
            {
                var ownVisiblePerson = new VisiblePerson
                {
                    userName = selfProfile.OwnProfile?.Name ?? "Unknown",
                    userAddress = selfProfile.OwnProfile?.UserId ?? "Unknown",
                    isGuest = false,
                    wearables = FilterNonBaseWearables(selfProfile.OwnProfile?.Avatar.Wearables ?? Array.Empty<URN>()),
                };

                visiblePeople.Add(ownVisiblePerson);
            }
        }

        [Query]
        private void CollectVisiblePeople(Profile profile, RemoteAvatarCollider avatarCollider, [Data] Plane[] frustumPlanes)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, avatarCollider.Collider.bounds))
            {
                var visiblePerson = new VisiblePerson
                {
                    userName = profile?.Name ?? "Unknown",
                    userAddress = profile?.UserId ?? "Unknown",
                    isGuest = false,
                    wearables = FilterNonBaseWearables(profile?.Avatar.Wearables ?? Array.Empty<URN>()),
                };

                visiblePeople.Add(visiblePerson);
            }
        }

        private static string[] FilterNonBaseWearables(IReadOnlyCollection<URN> avatarWearables)
        {
            var wearables = new List<string>();

            foreach (URN w in avatarWearables)
                if (!w.IsBaseWearable())
                    wearables.Add(w.ToString());

            return wearables.ToArray();
        }

        private async UniTask CollectMetadata()
        {
            Vector2Int sceneParcel = World.Get<CharacterTransform>(playerEntity).Position.ToParcel();

            string? sceneName = await GetSceneNameAsync(sceneParcel);

            ScreenshotMetadata? screenshotMetadata = ScreenshotMetadataProcessor.Create(selfProfile.OwnProfile, realmData, sceneParcel, sceneName, visiblePeople.ToArray());

            hud.Metadata = screenshotMetadata;
        }

        private async UniTask<string> GetSceneNameAsync(Vector2Int at)
        {
            if (realmData.ScenesAreFixed)
                return realmData.RealmName.Replace(".dcl.eth", string.Empty);

            PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(at, default(CancellationToken));

            return placeInfo?.title ?? "Unknown place";
        }
    }
}
