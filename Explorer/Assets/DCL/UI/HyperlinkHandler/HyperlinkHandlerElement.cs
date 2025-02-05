using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Profiles;
using MVC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.HyperlinkHandler
{
    public class HyperlinkHandlerElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IViewWithGlobalDependencies
    {
        //In the future we might need to migrate all these settings into a configurable SO
        private const string SCENE = "scene";
        private const string WORLD = "world";
        private const string URL = "url";
        private const string USER = "user";

        [SerializeField] private TMP_Text textComponent;
        [SerializeField] private TMP_StyleSheet styleSheet;
        [SerializeField] private string realmChangeConfirmationMessage = "Are you sure you want to enter this World?";

        private readonly Dictionary<string, Action<string>> linkHandlers = new ();
        private readonly StringBuilder stringBuilder = new ();

        private ViewDependencies dependencies;
        private bool initialized;
        private bool isHighlighting;
        private bool isHovering;
        private int lastHighlightedIndex = -1;
        private string originalText;
        private TMP_Style linkSelectedStyle;

        private void Awake()
        {
            AddLinkHandlers();
            linkSelectedStyle = styleSheet.GetStyle("LinkSelected");
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
            ResetPreviousHighlight();
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
                ResetPreviousHighlight();
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            this.dependencies = dependencies;
            initialized = true;
        }

        private void AddLinkHandlers()
        {
            linkHandlers.Add(URL, HandleURLLink);
            linkHandlers.Add(WORLD, HandleWorldLink);
            linkHandlers.Add(SCENE, HandleSceneLink);
            linkHandlers.Add(USER, HandleUserLink);
        }

        private void ProcessLink(string linkID)
        {
            // Expected format is "linkType=linkValue"
            string[] linkParts = linkID.Split('=');

            if (linkParts.Length != 2)
            {
                ReportHub.LogWarning(ReportCategory.UI, $"Invalid link format: {linkID}");
                return;
            }

            string linkType = linkParts[0].ToLower();
            string linkValue = linkParts[1];

            if (linkHandlers.TryGetValue(linkType, out Action<string> linkHandler))
                linkHandler.Invoke(linkValue);
            else
                ReportHub.LogWarning(ReportCategory.UI, $"No handler found for link: {linkID}");
        }

        private void HandleURLLink(string url)
        {
            //if URL doesn't have http:// at beginning we add it here
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            OpenUrlAsync(url).Forget();
        }

        private void HandleWorldLink(string sceneName)
        {
            ChangeRealmAsync(realmChangeConfirmationMessage, sceneName).Forget();
        }

        private void HandleSceneLink(string itemId)
        {
            string[] splitCords = itemId.Split(',');
            var coords = new Vector2Int(int.Parse(splitCords[0]), int.Parse(splitCords[1]));
            TeleportAsync(coords).Forget();
        }

        private void HandleUserLink(string userId)
        {
            OpenUserProfileContextMenu(userId).Forget();
        }

        private async UniTask OpenUserProfileContextMenu(string userId)
        {
            Profile profile = await dependencies.ProfileRepository.GetAsync(userId, ct: new CancellationToken());

            if (profile == null) return;

            var color = dependencies.ProfileNameColorHelper.GetNameColor(profile.Name);

            await dependencies.GlobalUIViews.ShowUserProfileContextMenu(profile, color, this.transform);
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

        private void HighlightCurrentLink(int linkIndex)
        {
            lastHighlightedIndex = linkIndex;
            isHighlighting = true;
            dependencies.Cursor.SetStyle(CursorStyle.Interaction, true);

            TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[linkIndex];

            int startIndex = linkInfo.linkIdFirstCharacterIndex + linkInfo.linkIdLength + 1;
            int endIndex = startIndex + linkInfo.linkTextLength;

            originalText = textComponent.text;
            stringBuilder.Clear();

            stringBuilder.Append(originalText.AsSpan(0, startIndex))
                         .Append(linkSelectedStyle.styleOpeningDefinition)
                         .Append(originalText.AsSpan(startIndex, linkInfo.linkTextLength))
                         .Append(linkSelectedStyle.styleClosingDefinition)
                         .Append(originalText.AsSpan(endIndex));

            textComponent.text = stringBuilder.ToString();
        }

        private void ResetPreviousHighlight()
        {
            if (lastHighlightedIndex < 0) return;

            textComponent.text = originalText;
            dependencies.Cursor.SetStyle(CursorStyle.Normal);
            lastHighlightedIndex = -1;
            isHighlighting = false;
            originalText = null;
        }
    }
}
