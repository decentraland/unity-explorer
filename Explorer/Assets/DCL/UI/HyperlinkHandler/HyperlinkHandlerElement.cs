using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using DCL.Input;
using DCL.TeleportPrompt;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.HyperlinkHandler
{
    public class HyperlinkHandlerElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private TMP_Text textComponent;

        private readonly Dictionary<string, Action<string>> linkHandlers = new ();
        private bool initialized;
        private bool isHovering;
        private bool isHighlighting;

        private HyperlinkHandlerDependencies dependencies;

        private void Awake()
        {
            AddLinkHandlers();
        }

        public void Initialize(HyperlinkHandlerDependencies dependencies)
        {
            this.dependencies = dependencies;
            initialized = true;
        }

        private void OnEnable()
        {
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            dependencies.Cursor.SetStyle(CursorStyle.Normal);
            isHovering = false;
            isHighlighting = false;
            //restore pointer and text
        }

        private void AddLinkHandlers()
        {
            linkHandlers.Add("url", HandleURLLink);
            linkHandlers.Add("world", HandleWorldLink);
            linkHandlers.Add("scene", HandleSceneLink);
        }

        private void OnMouseOver()
        {
            throw new NotImplementedException();
        }

        private void ProcessLink(string linkID)
        {
            // Expected format: "linkType:linkValue", we force the count to 2 as URLs will come in format https://
            string[] linkParts = linkID.Split(':', 2);

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
            //if URL doesn't have https: at beginning, add it here
            OpenUrlAsync(url).Forget();
        }

        private void HandleWorldLink(string sceneName)
        {
            ChangeRealmAsync("Are ya sure ya wanna go there?", sceneName).Forget();
        }

        private void HandleSceneLink(string itemId)
        {
            string[] splitCords = itemId.Split(',');
            var coords = new Vector2Int(int.Parse(splitCords[0]), int.Parse(splitCords[1]));
            TeleportAsync(coords).Forget();
        }

        private async UniTask OpenUrlAsync(string url) =>
            await dependencies.MvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));

        private async UniTask TeleportAsync(Vector2Int coords)
        {
            await UniTask.SwitchToMainThread();
            await dependencies.MvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(coords)));
        }

        private async UniTask ChangeRealmAsync(string message, string realm)
        {
            await UniTask.SwitchToMainThread();
            await dependencies.MvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(message, realm)));
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isHovering) return;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, null);

            if (linkIndex != -1)
            {
                if (isHighlighting) return;

                isHighlighting = true;
                var wordInfo = textComponent.textInfo.wordInfo[linkIndex];
                dependencies.Cursor.SetStyle(CursorStyle.Interaction, true);

                //apply TMPro style "LinkSelected" to the link text
                return;
            }

            isHighlighting = false;
            dependencies.Cursor.SetStyle(CursorStyle.Normal);
            //apply TMPro style "Link" to the link Text

        }
    }
}
