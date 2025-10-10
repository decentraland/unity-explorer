using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    public class SocialEmoteOutcomeMenuView : ViewBase, IView
    {
        [SerializeField]
        private CanvasGroup mainCanvasGroup;

        [SerializeField]
        private CanvasGroup outcomeListCanvasGroup;

        [SerializeField]
        private RectTransform outcomeList;

        [SerializeField]
        private RectTransform mainContainer;

        [SerializeField]
        private RectTransform distanceMessageContainer;

        [SerializeField]
        private TMP_Text distanceMessageText;

        [SerializeField]
        private TMP_Text emoteTitleText;

        [SerializeField]
        private SocialEmoteOutcomeChoiceView outcomeChoicePrefab;

        [SerializeField]
        private string distanceMessageTextTemplate = "Get closer to <USER> to Emote with them";

        [SerializeField]
        private float outcomeListAlphaWhenTooFar = 0.3f;

        SocialEmoteOutcomeChoiceView[] outcomeItems = new SocialEmoteOutcomeChoiceView[3];
        private int enabledOutcomes;

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            await mainCanvasGroup.DOFade(1.0f, 0.3f).AsyncWaitForCompletion();
        }

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            await mainCanvasGroup.DOFade(0.0f, 0.3f).AsyncWaitForCompletion();
        }

        private void Start()
        {
            for (int i = 0; i < outcomeItems.Length; ++i)
            {
                outcomeItems[i] = Instantiate(outcomeChoicePrefab, outcomeList);
                outcomeItems[i].gameObject.SetActive(false);
            }
        }

        public void ShowDistanceMessage(string username, Color usernameColor)
        {
            distanceMessageContainer.gameObject.SetActive(true);
            distanceMessageText.text = distanceMessageTextTemplate.Replace("<USER>", $"<color=#{ColorUtility.ToHtmlStringRGBA(usernameColor)}>{username}</color>");

            outcomeListCanvasGroup.alpha = outcomeListAlphaWhenTooFar;
        }

        public void HideDistanceMessage()
        {
            distanceMessageContainer.gameObject.SetActive(false);
            outcomeListCanvasGroup.alpha = 1.0f;
        }

        public void AddChoice(string title)
        {
            if (enabledOutcomes == outcomeItems.Length)
            {
                ReportHub.LogError(ReportCategory.EMOTE, "The maximum amount of outcomes was reached, it's not possible to add more choices to the menu.");
                return;
            }

            outcomeItems[enabledOutcomes].gameObject.SetActive(true);
            outcomeItems[enabledOutcomes].SetTitle(enabledOutcomes, title);
            enabledOutcomes++;
        }

        public void RemoveAllChoices()
        {
            for(int i = 0; i < outcomeItems.Length; ++i)
            {
                outcomeItems[i].gameObject.SetActive(false);
            }

            enabledOutcomes = 0;
        }

        public void SetEmoteTitle(string emoteName)
        {
            emoteTitleText.text = emoteName;
        }
    }
}
