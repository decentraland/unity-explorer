using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    public class PreConfiguredTipView : TipView
    {
        public override void Set(SceneTips.LoadedTip tip, Sprite[] fallbackSprites)
        {
            // The prefab is already preconfigured with the tip information, no need to do anything
        }
    }
}
