using Cysharp.Threading.Tasks;
using DCL.Billboard.Demo.Properties;
using DCL.DemoWorlds;
using DCL.SDKComponents.NftShape.Component;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class NftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private BillboardProperties billboardProperties = new ();
        [SerializeField]
        private bool visible = true;

        private void Start()
        {
            new WarmUpSettingsNftShapeDemoWorld(nftShapeProperties, billboardProperties, () => visible)
               .SetUpAndRunAsync(destroyCancellationToken)
               .Forget();
        }
    }
}
