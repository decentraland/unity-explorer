using MVC;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace DCL.SceneLoadingScreens
{
    public class SceneLoadingScreenView : ViewBase, IView
    {
        [field: SerializeField]
        public Slider ProgressBar { get; private set; } = null!;

        [field: SerializeField]
        public LocalizeStringEvent ProgressLabel { get; private set; } = null!;

        [field: SerializeField]
        public Button ShowNextButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ShowPreviousButton { get; private set; } = null!;

        [SerializeField]
        private Transform tipsParent = null!;

        [SerializeField]
        private TipView tipViewPrefab = null!;

        [SerializeField]
        private Sprite[] fallbackSprites = null!;

        [SerializeField]
        private Image backgroundImage = null!;

        private readonly List<TipView> tips = new ();

        public void ClearTips()
        {
            foreach (TipView tip in tips)
                Destroy(tip.gameObject);

            tips.Clear();
        }

        public void AddTip(SceneTips.Tip tip)
        {
            TipView view = Instantiate(tipViewPrefab, tipsParent);
            view.TitleLabel.text = tip.Title;
            view.BodyLabel.text = tip.Body;

            Texture2D? texture = tip.Image;

            view.Image.sprite = texture != null
                ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f))
                : fallbackSprites[Random.Range(0, fallbackSprites.Length)];

            tips.Add(view);
        }

        public void ShowTip(int index)
        {
            foreach (TipView? view in tips)
                view.gameObject.SetActive(false);

            tips[index].gameObject.SetActive(true);
            backgroundImage.sprite = tips[index].Image.sprite;
        }
    }
}
