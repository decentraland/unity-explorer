using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    [RequireComponent(typeof(UIDocument))]
    public class NametagHolder: MonoBehaviour
    {
        public NametagElement Nametag { get; private set; }

        private void OnEnable() =>
            Nametag = GetComponent<UIDocument>().rootVisualElement.Q<NametagElement>();
    }
}
