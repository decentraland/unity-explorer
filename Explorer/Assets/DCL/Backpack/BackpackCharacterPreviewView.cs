using UnityEngine;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewView : MonoBehaviour
    {
        [field: SerializeField] public RawImage RawImage { get; private set; }
        [field: SerializeField] public CharacterPreviewContainer CharacterPreviewContainer { get; private set;}

        public void Initialize()
        {
            CharacterPreviewContainer.Initialize((RenderTexture)RawImage.texture);
        }
    }
}
