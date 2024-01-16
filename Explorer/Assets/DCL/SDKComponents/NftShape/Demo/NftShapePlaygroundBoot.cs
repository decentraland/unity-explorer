using Cysharp.Threading.Tasks;
using DCL.DemoWorlds;
using DCL.SDKComponents.NftShape.Component;
using DCL.SDKComponents.NftShape.Frames.Pool;
using DCL.Utilities.Extensions;
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
            new WarmUpSettingsNftShapeDemoWorld(new FramesPool(settings.EnsureNotNull()), nftShapeProperties, () => visible)
               .SetUpAndRunAsync(destroyCancellationToken)
               .Forget();
        }
    }
}
