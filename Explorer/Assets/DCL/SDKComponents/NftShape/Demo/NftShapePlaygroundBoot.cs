using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DemoWorlds;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frames.FramePrefabs;
using DCL.SDKComponents.NftShape.Frames.Pool;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class NftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private NftShapeSettings settings = null!;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTask LaunchAsync()
        {
            var framesPrefabs = new AssetProvisionerFramePrefabs(new AddressablesProvisioner());
            var world = new WarmUpSettingsNftShapeDemoWorld(new FramesPool(framesPrefabs), nftShapeProperties, () => visible);

            await framesPrefabs.Initialize(
                settings.FramePrefabs(),
                settings.DefaultFrame(),
                destroyCancellationToken
            );

            await world.SetUpAndRunAsync(destroyCancellationToken);
        }
    }
}
