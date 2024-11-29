using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.Input;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.Settings;
using DCL.InWorldCamera.UI;
using DCL.PlacesAPIService;
using DCL.Profiles.Self;
using ECS;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using static DCL.PluginSystem.Global.InWorldCameraPlugin;
using CaptureScreenshotSystem = DCL.InWorldCamera.Systems.CaptureScreenshotSystem;
using EmitInWorldCameraInputSystem = DCL.InWorldCamera.Systems.EmitInWorldCameraInputSystem;
using MoveInWorldCameraSystem = DCL.InWorldCamera.Systems.MoveInWorldCameraSystem;
using ToggleInWorldCameraActivitySystem = DCL.InWorldCamera.Systems.ToggleInWorldCameraActivitySystem;

namespace DCL.PluginSystem.Global
{
    public class InWorldCameraPlugin : IDCLGlobalPlugin<InWorldCameraSettings>
    {
        private readonly DCLInput input;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly SelfProfile selfProfile;
        private readonly RealmData realmData;
        private readonly Entity playerEntity;
        private readonly IPlacesAPIService placesAPIService;
        private readonly ICharacterObject characterObject;
        private readonly ICoroutineRunner coroutineRunner;
        private readonly InWorldCameraFactory factory;
        private readonly IMVCManager mvcManager;
        private readonly ICursor cursor;
        private readonly Button sidebarButton;
        private readonly Arch.Core.World globalWorld;
        private readonly ICameraReelStorageService cameraReelStorageService;

        private ScreenRecorder recorder;
        private GameObject hud;
        private ScreenshotMetadataBuilder metadataBuilder;
        private InWorldCameraSettings settings;
        private InWorldCameraController inWorldCameraController;
        private CharacterController followTarget;

        public InWorldCameraPlugin(DCLInput input, SelfProfile selfProfile,
            RealmData realmData, Entity playerEntity,
            IPlacesAPIService placesAPIService,
            ICharacterObject characterObject, ICoroutineRunner coroutineRunner,
            CameraReelRemoteStorageService cameraReelRemoteStorageService,
            IMVCManager mvcManager,
            ICursor cursor,
            Button sidebarButton,
            Arch.Core.World globalWorld
       )
        {
            this.input = input;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.playerEntity = playerEntity;
            this.placesAPIService = placesAPIService;
            this.characterObject = characterObject;
            this.coroutineRunner = coroutineRunner;
            this.cameraReelStorageService = cameraReelRemoteStorageService;
            this.mvcManager = mvcManager;
            this.cursor = cursor;
            this.sidebarButton = sidebarButton;
            this.globalWorld = globalWorld;

            factory = new InWorldCameraFactory();
        }

        public void Dispose()
        {
            factory.Dispose();
        }

        public UniTask InitializeAsync(InWorldCameraSettings settings, CancellationToken ct)
        {
            this.settings = settings;

            hud = factory.CreateScreencaptureHud(settings.ScreencaptureHud);
            followTarget = factory.CreateFollowTarget(settings.FollowTarget);

            recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
            metadataBuilder = new ScreenshotMetadataBuilder(selfProfile, characterObject.Controller, realmData, placesAPIService);

            inWorldCameraController = new InWorldCameraController(() => hud.GetComponent<InWorldCameraView>(), sidebarButton, globalWorld, mvcManager, cameraReelStorageService);
            mvcManager.RegisterController(inWorldCameraController);

            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ToggleInWorldCameraActivitySystem.InjectToWorld(ref builder, settings.TransitionSettings, inWorldCameraController, followTarget, cursor, mvcManager);
            EmitInWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera, input.Shortcuts.ToggleInWorldCamera);
            MoveInWorldCameraSystem.InjectToWorld(ref builder, settings.MovementSettings, characterObject.Controller.transform, cursor);
            CaptureScreenshotSystem.InjectToWorld(ref builder, recorder, playerEntity, metadataBuilder, coroutineRunner, cameraReelStorageService, inWorldCameraController);
        }

        [Serializable]
        public class InWorldCameraSettings : IDCLPluginSettings
        {
            [field: Header(nameof(InWorldCameraSettings))]
            [field: SerializeField] internal GameObject ScreencaptureHud { get; private set; }
            [field: SerializeField] internal GameObject FollowTarget { get; private set; }

            [field: Header("Configs")]
            [field: SerializeField] internal InWorldCameraTransitionSettings TransitionSettings { get; private set; }
            [field: SerializeField] internal InWorldCameraMovementSettings MovementSettings { get; private set; }
        }
    }
}
