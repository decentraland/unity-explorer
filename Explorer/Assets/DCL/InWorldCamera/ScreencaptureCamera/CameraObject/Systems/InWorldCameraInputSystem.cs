using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
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

        protected override void Update(float t)
        {
            EmitInputQuery(World);
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
                CollectMetadata().Forget();
            }
        }

        private async UniTask CollectMetadata()
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

            hud.Metadata = ScreenshotMetadataProcessor.Create(ownProfile, realmData, parcel, sceneName);
            Debug.Log("VVV 1");
        }

        private void Show(Texture2D screenshot)
        {
            hud.Screenshot = screenshot;
            hud.GetComponent<Canvas>().enabled = true;
        }
    }
}
