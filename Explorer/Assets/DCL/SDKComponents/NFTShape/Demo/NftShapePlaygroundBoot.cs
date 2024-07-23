using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DemoWorlds;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class NftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private NFTShapeSettings settings = null!;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTask LaunchAsync()
        {
            var framesPrefabs = new AssetProvisionerFramePrefabs(new AddressablesProvisioner());
            var world = new WarmUpSettingsNftShapeDemoWorld(new FramesPool(framesPrefabs), framesPrefabs, nftShapeProperties, () => visible);

            await framesPrefabs.InitializeAsync(
                settings.FramePrefabs(),
                settings.DefaultFrame(),
                destroyCancellationToken
            );

            await world.SetUpAndRunAsync(destroyCancellationToken);
        }
    }
}
