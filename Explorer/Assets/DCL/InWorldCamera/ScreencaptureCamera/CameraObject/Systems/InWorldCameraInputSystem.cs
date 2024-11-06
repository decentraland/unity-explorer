using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;
using static DCL.InWorldCamera.ScreencaptureCamera.CameraObject.InWorldCameraComponents;
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

        private ScreenRecorder recorder;

        private bool isInstantiated;
        private ScreenshotHudView hud;

        private Profile? ownProfile;

        public InWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hudPrefab, SelfProfile selfProfile, RealmData realmData,
            Entity playerEntity, IPlacesAPIService placesAPIService) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hudPrefab = hudPrefab;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
        }

        public override void Initialize()
        {
            base.Initialize();
            camera = World.CacheCamera();
        }

        List<VisiblePerson> visiblePeople = new List<VisiblePerson>();

        protected override void Update(float t)
        {
            EmitInputQuery(World);

            if (isMakingScreenshot)
            {
                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera.GetCameraComponent(World).Camera);

                // Check self
                // if (GeometryUtility.TestPlanesAABB(frustumPlanes, player.collider.bounds)) list.Add(player);

                visiblePeople.Clear();

                CollectVisiblePeopleQuery(World, frustumPlanes);
                CollectMetadata(visiblePeople.ToArray()).Forget();
                isMakingScreenshot = false;
            }
        }

        [Query]
        private void CollectVisiblePeople(in Entity entity, Profile profile, RemoteAvatarCollider avatarCollider, [Data] Plane[] frustumPlanes)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, avatarCollider.Collider.bounds))
            {
                VisiblePerson visiblePerson = new VisiblePerson
                {
                    userName = profile.Name,
                    userAddress = profile.UserId,
                    isGuest = false,
                    wearables = FilterNonBaseWearables(profile.Avatar.Wearables)
                };

                visiblePeople.Add(visiblePerson);
            }
        }

        private static string[] FilterNonBaseWearables(IReadOnlyCollection<URN> avatarWearables)
        {
            List<string> wearables = new List<string>();

            foreach (URN w in avatarWearables)
                if(w.IsThirdPartyCollection())
                    wearables.Add(w.ToString());

            return wearables.ToArray();
        }

        [Query]
        [All(typeof(IsInWorldCamera))]
        private void EmitInput(in Entity entity)
        {
            if (!isInstantiated)
            {
                hud = Object.Instantiate(hudPrefab, Vector3.zero, Quaternion.identity).GetComponent<ScreenshotHudView>();
                recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());

                isInstantiated = true;
            }

            if (isInstantiated && inputSchema.Screenshot.triggered)
            {
                hud.GetComponent<Canvas>().enabled = false;
                hud.StartCoroutine(recorder.CaptureScreenshot(Show));
                isMakingScreenshot = true;
            }
        }

        private bool isMakingScreenshot = false;
        private SingleInstanceEntity camera;

        private async UniTask CollectMetadata(VisiblePerson[] visiblePeople)
        {
            ownProfile ??= await selfProfile.ProfileAsync(default(CancellationToken));

            var parcel = World.Get<CharacterTransform>(playerEntity).Position.ToParcel();

            string sceneName;
            if (realmData.ScenesAreFixed)
                sceneName = realmData.RealmName.Replace(".dcl.eth", string.Empty);
            else
            {
                PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(parcel, default(CancellationToken));
                sceneName = placeInfo?.title ?? "Unknown place";
            }

            hud.Metadata = ScreenshotMetadataProcessor.Create(ownProfile, realmData, parcel, sceneName, visiblePeople);
            Debug.Log("VVV 1");
        }

        private void Show(Texture2D screenshot)
        {
            hud.Screenshot = screenshot;
            hud.GetComponent<Canvas>().enabled = true;
        }
    }
}
