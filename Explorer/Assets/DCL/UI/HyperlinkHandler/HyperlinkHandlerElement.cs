using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.HyperlinkHandler
{
    public class HyperlinkHandlerElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text textComponent;

        private readonly Dictionary<string, Action<string>> linkHandlers = new ();

        private HyperlinkHandlerSettings settings;
        private bool initialized = false;
        public void Initialize(HyperlinkHandlerSettings settings)
        {
            this.settings = settings;
            initialized = true;
        }

        private void Awake()
        {
            AddLinkHandlers();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!initialized) return;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, null);

            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[linkIndex];
                string linkID = linkInfo.GetLinkID();
                ProcessLink(linkID);
            }
        }

        private void AddLinkHandlers()
        {
            linkHandlers.Add("url", HandleURLLink);
            linkHandlers.Add("world", HandleWorldLink);
            linkHandlers.Add("scene", HandleSceneLink);
        }

        private void ProcessLink(string linkID)
        {
            // Expected format: "linkType:linkValue"
            string[] linkParts = linkID.Split(':');

            if (linkParts.Length != 2)
            {
                ReportHub.LogWarning(ReportCategory.UI, $"Invalid link format: {linkID}");
                return;
            }

            string linkType = linkParts[0].ToLower();
            string linkValue = linkParts[1];

            if (linkHandlers.TryGetValue(linkType, out Action<string>? linkHandler)) { linkHandler.Invoke(linkValue); }
            else
                ReportHub.LogWarning(ReportCategory.UI, $"No handler found for link: {linkID}");
        }

        private void HandleURLLink(string url)
        {
            OpenUrlAsync(url).Forget();
        }

        private async UniTask OpenUrlAsync(string url) =>
            await settings.MvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));

        private void HandleWorldLink(string sceneName)
        {
            Debug.Log($"Loading World: {sceneName}");
        }

        private void HandleSceneLink(string itemId)
        {
            Debug.Log($"Loading Scene: {itemId}");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, null);

            if (linkIndex != -1)
            {
                //change pointer to selection pointer and underline text
            }
            else
            {
                //restore pointer and text
            }

        }

        public void OnPointerExit(PointerEventData eventData)
        {
            //restore pointer and text
        }
    }
}
