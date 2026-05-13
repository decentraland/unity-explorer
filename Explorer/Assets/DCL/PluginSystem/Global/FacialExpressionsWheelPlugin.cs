using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.FacialExpression;
using DCL.CharacterPreview;
using DCL.FacialExpressionsWheel;
using DCL.Input;
using DCL.Profiles.Self;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class FacialExpressionsWheelPlugin : IDCLGlobalPlugin<FacialExpressionsWheelPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly SelfProfile selfProfile;
        private readonly IInputBlock inputBlock;
        private readonly ICursor cursor;
        private readonly IEventBus eventBus;
        private readonly IMVCManager mvcManager;
        private readonly Arch.Core.World world;
        private readonly Arch.Core.Entity playerEntity;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;

        private FacialExpressionsWheelController? wheelController;
        private IDisposable? toggleSubscription;

        public FacialExpressionsWheelPlugin(
            IAssetsProvisioner assetsProvisioner,
            SelfProfile selfProfile,
            IInputBlock inputBlock,
            ICursor cursor,
            IEventBus eventBus,
            IMVCManager mvcManager,
            Arch.Core.World world,
            Arch.Core.Entity playerEntity,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;
            this.cursor = cursor;
            this.eventBus = eventBus;
            this.mvcManager = mvcManager;
            this.world = world;
            this.playerEntity = playerEntity;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
        }

        public void Dispose()
        {
            toggleSubscription?.Dispose();
            wheelController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            FacialExpressionsWheelView wheelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.WheelPrefab, ct))
                                                   .Value.GetComponent<FacialExpressionsWheelView>();

            AvatarFaceExpressionConfig expressionConfig = (await assetsProvisioner.ProvideMainAssetAsync(settings.ExpressionConfig, ct)).Value;

            ControllerBase<FacialExpressionsWheelView>.ViewFactoryMethod viewFactory =
                FacialExpressionsWheelController.Preallocate(wheelPrefab, null, out FacialExpressionsWheelView wheelView);

            var previewController = new FacialExpressionsCharacterPreviewController(
                wheelView.CharacterPreview,
                characterPreviewFactory,
                world,
                characterPreviewEventBus);

            wheelController = new FacialExpressionsWheelController(
                viewFactory,
                selfProfile,
                expressionConfig,
                world,
                playerEntity,
                inputBlock,
                cursor,
                eventBus,
                previewController);

            mvcManager.RegisterController(wheelController);

            toggleSubscription = eventBus.Subscribe<RequestToggleFacialExpressionsWheelEvent>(OnToggle);
        }

        private void OnToggle(RequestToggleFacialExpressionsWheelEvent _)
        {
            if (wheelController == null) return;

            if (wheelController.State == ControllerState.ViewHidden)
                mvcManager.ShowAndForget(FacialExpressionsWheelController.IssueCommand());
            else
                wheelController.Close();
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceGameObject WheelPrefab { get; set; } = null!;
            [field: SerializeField] public AssetReferenceT<AvatarFaceExpressionConfig> ExpressionConfig { get; set; } = null!;
        }
    }
}