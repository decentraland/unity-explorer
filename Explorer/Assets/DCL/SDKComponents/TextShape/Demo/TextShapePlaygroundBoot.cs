using Cysharp.Threading.Tasks;
using DCL.Billboard.Demo.Properties;
using DCL.DemoWorlds;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts.Settings;
using DCL.Utilities.Extensions;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class TextShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField] private SoFontList fontList = null!;
        [SerializeField]
        private TextShapeProperties textShapeProperties = new ();
        [SerializeField]
        private BillboardProperties billboardProperties = new ();
        [SerializeField]
        private bool visible = true;

        private void Start()
        {
            new WarmUpSettingsTextShapeDemoWorld(textShapeProperties, billboardProperties, () => visible, fontList.EnsureNotNull())
               .SetUpAndRunAsync(destroyCancellationToken)
               .Forget();
        }
    }
}
