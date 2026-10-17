using RichTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens
{
    public class TipView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TitleLabel { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text BodyLabel { get; private set; } = null!;

        [field: SerializeField]
        public Image Image { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup RootCanvasGroup { get; private set; } = null!;

        public virtual void Set(SceneTips.LoadedTip tip)
        {
            TitleLabel.text = tip.Title;
            BodyLabel.text = tip.Body;

            Option<Sprite> spriteResource = tip.Image.Resource;

            Sprite? icon = spriteResource.Has
                ? spriteResource.Value
                : null;

            Image.sprite = icon;
        }
    }
}
