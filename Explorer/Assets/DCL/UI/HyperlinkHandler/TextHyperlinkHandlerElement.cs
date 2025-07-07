using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.UI.Utilities;
using MVC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.UI.HyperlinkHandler
{
    /// <summary>
    /// Adding this component into an object that contains a TMP_Text, will allow it to handle hyperlinks, both for hover behaviour and also clicking on the links themselves.
    /// </summary>
    public class TextHyperlinkHandlerElement : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const string LINK_SELECTED_OPENING_STYLE = "<u>";
        private const string LINK_SELECTED_CLOSING_STYLE = "</u>";
        private const string REALM_CHANGE_CONFIRMATION_MESSAGE = "Are you sure you want to enter this World?";

        [SerializeField] private TMP_Text textComponent;

        private readonly Dictionary<string, Action<string>> linkHandlers = new ();
        private readonly StringBuilder stringBuilder = new ();

        private bool initialized;
        private bool isHighlighting;
        private bool isHovering;
        private int lastHighlightedIndex = -1;
        private string originalText;
        private TMP_LinkInfo lastLink;
        private CancellationTokenSource cancellationTokenSource;
        private UniTaskCompletionSource closeContextMenuTask;

        private void Awake()
        {
            AddLinkHandlers();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (lastHighlightedIndex != -1)
            {
                cancellationTokenSource = cancellationTokenSource.SafeRestart();
                TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[lastHighlightedIndex];
                lastLink = linkInfo;
                string linkType = linkInfo.GetLinkID();
                string linkText = linkInfo.GetLinkText();
                ProcessLink(linkType, linkText);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ViewDependencies.Cursor.SetStyle(CursorStyle.Normal);
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
        
        private void AddLinkHandlers()
        {
            linkHandlers.Add(HyperlinkConstants.URL, HandleURLLink);
            linkHandlers.Add(HyperlinkConstants.WORLD, HandleWorldLink);
            linkHandlers.Add(HyperlinkConstants.SCENE, HandleSceneLink);
            linkHandlers.Add(HyperlinkConstants.PROFILE, HandleUserLink);
        }

        private void ProcessLink(string linkType, string linkValue)
        {
            if (linkHandlers.TryGetValue(linkType, out Action<string> linkHandler))
                linkHandler.Invoke(linkValue);
            else
                ReportHub.LogWarning(ReportCategory.UI, $"No handler found for link: {linkType}");
        }

        private void HandleURLLink(string url)
        {
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            OpenUrlAsync(URLAddress.FromString(url), cancellationTokenSource.Token).Forget();
        }

        private void HandleWorldLink(string sceneName)
        {
            ChangeRealmAsync(REALM_CHANGE_CONFIRMATION_MESSAGE, sceneName, cancellationTokenSource.Token).Forget();
        }

        private void HandleSceneLink(string itemId)
        {
            string[] splitCords = itemId.Split(',');
            var coords = new Vector2Int(int.Parse(splitCords[0]), int.Parse(splitCords[1]));
            TeleportAsync(coords, cancellationTokenSource.Token).Forget();
        }

        private void HandleUserLink(string userName)
        {
            OpenUserProfileContextMenuAsync(userName.Substring(1)).Forget();
        }

        private Vector2 GetLastCharacterPosition(TMP_LinkInfo linkInfo)
        {
            int lastCharacterIndex = linkInfo.linkTextfirstCharacterIndex + linkInfo.linkTextLength - 1;

            TMP_CharacterInfo cInfo = textComponent.textInfo.characterInfo[lastCharacterIndex];

            Vector3 bottomRight = cInfo.bottomRight;

            RectTransform rectTransform = textComponent.rectTransform;
            Vector3 lastCharacterPosition = rectTransform.TransformPoint(bottomRight);

            return lastCharacterPosition;
        }

        private async UniTaskVoid OpenUserProfileContextMenuAsync(string userName)
        {
            closeContextMenuTask?.TrySetResult();
            closeContextMenuTask = new UniTaskCompletionSource();
            await ViewDependencies.GlobalUIViews.ShowUserProfileContextMenuFromUserNameAsync(userName, GetLastCharacterPosition(lastLink), default(Vector2), cancellationTokenSource.Token, closeContextMenuTask.Task);
        }

        private async UniTaskVoid OpenUrlAsync(URLAddress url, CancellationToken ct) =>
            await ViewDependencies.GlobalUIViews.ShowExternalUrlPromptAsync(url, ct);

        private async UniTaskVoid TeleportAsync(Vector2Int coords, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            await ViewDependencies.GlobalUIViews.ShowTeleporterPromptAsync(coords, ct);
        }

        private async UniTaskVoid ChangeRealmAsync(string message, string realm, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            await ViewDependencies.GlobalUIViews.ShowChangeRealmPromptAsync(message, realm, ct);
        }

        private void HighlightCurrentLink(int linkIndex)
        {
            lastHighlightedIndex = linkIndex;
            isHighlighting = true;
            ViewDependencies.Cursor.SetStyle(CursorStyle.Interaction, true);

            TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[linkIndex];

            int startIndex = linkInfo.linkIdFirstCharacterIndex + linkInfo.linkIdLength + 1;
            int endIndex = startIndex + linkInfo.linkTextLength;

            originalText = textComponent.text;
            stringBuilder.Clear();

            stringBuilder.Append(originalText.AsSpan(0, startIndex))
                         .Append(LINK_SELECTED_OPENING_STYLE)
                         .Append(originalText.AsSpan(startIndex, linkInfo.linkTextLength))
                         .Append(LINK_SELECTED_CLOSING_STYLE)
                         .Append(originalText.AsSpan(endIndex));

            textComponent.text = stringBuilder.ToString();
        }

        private void ResetPreviousHighlight()
        {
            if (lastHighlightedIndex < 0) return;

            textComponent.text = originalText;
            lastHighlightedIndex = -1;
            isHighlighting = false;
            originalText = null;
        }

        private void OnDisable()
        {
            closeContextMenuTask?.TrySetResult();
            cancellationTokenSource.SafeCancelAndDispose();
        }

        private void OnDestroy()
        {
            closeContextMenuTask?.TrySetResult();
            cancellationTokenSource.SafeCancelAndDispose();
        }
    }
}
