using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using MVC;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.HyperlinkHandler
{
    public class HyperlinkHandlerElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IViewWithGlobalDependencies
    {
        [SerializeField] private TMP_Text textComponent;
        [SerializeField] private TMP_StyleSheet styleSheet;

        private readonly Dictionary<string, Action<string>> linkHandlers = new ();
        private bool initialized;
        private bool isHovering;
        private bool isHighlighting;
        private int lastHighlightedIndex = -1;
        private string originalText;
        private TMP_Style selectedStyle;
        private StringBuilder stringBuilder;

        private ViewDependencies dependencies;
        private ICursor cursor;


        private void Awake()
        {
            AddLinkHandlers();
            selectedStyle = styleSheet.GetStyle("LinkSelected");
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            this.dependencies = dependencies;
            initialized = true;
        }

        private void OnEnable()
        {
            //LINKS SHOULD BE FORMATTED AND VALIDATED FROM WHEREVER THEY COME?
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

            if (linkHandlers.TryGetValue(linkType, out Action<string> linkHandler)) { linkHandler.Invoke(linkValue); }
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
            await dependencies.GlobalUIViews.ShowExternalUrlPromptAsync(url);

        private async UniTask TeleportAsync(Vector2Int coords)
        {
            await UniTask.SwitchToMainThread();
            await dependencies.GlobalUIViews.ShowTeleporterPromptAsync(coords);
        }

        private async UniTask ChangeRealmAsync(string message, string realm)
        {
            await UniTask.SwitchToMainThread();
            await dependencies.GlobalUIViews.ShowChangeRealmPromptAsync(message, realm);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!isHovering) return;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, null);

            if (linkIndex != -1)
            {
                if (isHighlighting && lastHighlightedIndex == linkIndex) return;

                ResetPreviousHighlight();
                HighlightCurrentLink(linkIndex);
                return;
            }

            if (isHighlighting)
            {
                ResetPreviousHighlight();
            }
        }

        private void HighlightCurrentLink(int linkIndex)
        {
            lastHighlightedIndex = linkIndex;
            isHighlighting = true;
            dependencies.Cursor.SetStyle(CursorStyle.Interaction, true);

            var linkInfo = textComponent.textInfo.linkInfo[linkIndex];

            int startIndex = linkInfo.linkIdFirstCharacterIndex + linkInfo.linkIdLength + 2;
            int endIndex = startIndex + linkInfo.linkTextLength;

            originalText = textComponent.text;
            stringBuilder.Clear();
            stringBuilder.Append(originalText).Insert(endIndex, selectedStyle.styleClosingDefinition).Insert(startIndex,selectedStyle.styleOpeningDefinition);
            textComponent.text = stringBuilder.ToString();
        }

        private void ResetPreviousHighlight()
        {
            if (lastHighlightedIndex >= 0)
            {
                textComponent.text = originalText;
                dependencies.Cursor.SetStyle(CursorStyle.Normal);
                lastHighlightedIndex = -1;
                isHighlighting = false;
            }
        }
    }
}
