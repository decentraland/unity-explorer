using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
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
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class InWorldCameraInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private readonly GameObject hudPrefab;
        private readonly SelfProfile selfProfile;
        private readonly RealmData realmData;
        private readonly Entity playerEntity;
        private readonly IPlacesAPIService placesAPIService;
        private readonly CharacterController characterObjectController;

        private ScreenRecorder recorder;

        private bool isInstantiated;
        private ScreenshotHudView hud;

        private Profile? ownProfile;

        private readonly List<VisiblePerson> visiblePeople = new ();

        private bool isMakingScreenshot;
        private SingleInstanceEntity camera;

        public InWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hudPrefab, SelfProfile selfProfile, RealmData realmData,
            Entity playerEntity, IPlacesAPIService placesAPIService, ICharacterObject characterObject) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hudPrefab = hudPrefab;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            characterObjectController = characterObject.Controller;
        }

        public override void Initialize()
        {
            base.Initialize();
            camera = World.CacheCamera();
            GetOwnProfileAsync().Forget();
        }

        private async UniTask GetOwnProfileAsync()
        {
            ownProfile ??= await selfProfile.ProfileAsync(default(CancellationToken));
        }

        protected override void Update(float t)
        {
            TakeScreenshotQuery(World);

            if (isMakingScreenshot)
            {
                visiblePeople.Clear();

                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera.GetCameraComponent(World).Camera);

                // Check self
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, characterObjectController.bounds))
                {
                    var ownVisiblePerson = new VisiblePerson
                    {
                        userName = ownProfile?.Name ?? "Unknown",
                        userAddress = ownProfile?.UserId ?? "Unknown",
                        isGuest = false,
                        wearables = FilterNonBaseWearables(ownProfile?.Avatar.Wearables ?? Array.Empty<URN>()),
                    };

                    visiblePeople.Add(ownVisiblePerson);
                }


                CollectVisiblePeopleQuery(World, frustumPlanes);
                CollectMetadata(visiblePeople.ToArray()).Forget();
                isMakingScreenshot = false;
            }

            EnableCameraQuery(World);
            DisableCameraQuery(World);
        }

        [Query]
        private void CollectVisiblePeople(Profile profile, RemoteAvatarCollider avatarCollider, [Data] Plane[] frustumPlanes)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, avatarCollider.Collider.bounds))
            {
                var visiblePerson = new VisiblePerson
                {
                    userName = profile.Name,
                    userAddress = profile.UserId,
                    isGuest = false,
                    wearables = FilterNonBaseWearables(profile.Avatar.Wearables),
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

        [Query]
        [None(typeof(IsInWorldCamera))]
        private void EnableCamera(Entity camera, ref CameraComponent cameraComponent)
        {
            if (inputSchema.ToggleInWorld!.triggered)
            {
                cameraComponent.Mode = CameraMode.InWorld;

                if (isInstantiated)
                    hud.gameObject.SetActive(true);
                else
                {
                    hud = Object.Instantiate(hudPrefab, Vector3.zero, Quaternion.identity).GetComponent<ScreenshotHudView>();
                    recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
                    isInstantiated = true;
                }

                World.Add<IsInWorldCamera>(camera);
            }
        }

        [Query]
        [All(typeof(IsInWorldCamera))]
        private void DisableCamera(Entity camera, ref CameraComponent cameraComponent)
        {
            if (inputSchema.ToggleInWorld!.triggered)
            {
                cameraComponent.Mode = CameraMode.ThirdPerson;
                hud.gameObject.SetActive(false);

                World.Remove<IsInWorldCamera>(camera);
            }
        }

        [Query]
        [All(typeof(IsInWorldCamera))]
        private void TakeScreenshot()
        {
            if (inputSchema.Screenshot.triggered)
            {
                hud.GetComponent<Canvas>().enabled = false;
                hud.StartCoroutine(recorder.CaptureScreenshot(Show));
                isMakingScreenshot = true;
            }
        }

        private async UniTask CollectMetadata(VisiblePerson[] visiblePeople)
        {
            await GetOwnProfileAsync();
            Vector2Int parcel = World.Get<CharacterTransform>(playerEntity).Position.ToParcel();

            string sceneName;

            if (realmData.ScenesAreFixed)
                sceneName = realmData.RealmName.Replace(".dcl.eth", string.Empty);
            else
            {
                PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(parcel, default(CancellationToken));
                sceneName = placeInfo?.title ?? "Unknown place";
            }

            hud.Metadata = ScreenshotMetadataProcessor.Create(ownProfile, realmData, parcel, sceneName, visiblePeople);
        }

        private void Show(Texture2D screenshot)
        {
            hud.Screenshot = screenshot;
            hud.GetComponent<Canvas>().enabled = true;
        }
    }
}
