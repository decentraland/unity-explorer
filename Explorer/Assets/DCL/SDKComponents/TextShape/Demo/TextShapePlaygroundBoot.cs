using Cysharp.Threading.Tasks;
using DCL.DemoWorlds;
using DCL.SDKComponents.TextShape.Component;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class TextShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private TextShapeProperties textShapeProperties = new ();
        [SerializeField]
        private bool visible = true;

        private void Start()
        {
            new WarmUpSettingsTextShapeDemoWorld(textShapeProperties, () => visible)
               .SetUpAndRunAsync(destroyCancellationToken)
               .Forget();
        }
    }
}
