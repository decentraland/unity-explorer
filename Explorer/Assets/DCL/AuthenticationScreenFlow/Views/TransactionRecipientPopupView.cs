using Cysharp.Threading.Tasks;
using DCL.Web3.Authenticators;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    ///     Transaction confirmation popup that states, in plain language, who receives the assets.
    ///     Lives as its own prefab nested inside the Web3 confirmation popup; the copy is picked from the
    ///     resolved <see cref="RecipientGate" /> (profile / scene creator / external wallet).
    /// </summary>
    public class TransactionRecipientPopupView : MonoBehaviour
    {
        // Link/highlight blue used for names and addresses in the confirmation copy.
        private const string HIGHLIGHT_COLOR = "#32CEFF";

        // MANA renders as a sprite (a sprite named "MANA" must exist in the description's TMP sprite asset).
        private const string MANA_SYMBOL = "MANA";

        [SerializeField] private TMP_Text description;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        public async UniTask<bool> ShowAsync(TransactionConfirmationRequest request, CancellationToken ct)
        {
            description.text = BuildDescription(request);
            gameObject.SetActive(true);

            var tcs = new UniTaskCompletionSource<bool>();

            cancelButton.onClick.AddListener(OnCancel);
            confirmButton.onClick.AddListener(OnConfirm);

            try { return await tcs.Task.AttachExternalCancellation(ct); }
            finally
            {
                cancelButton.onClick.RemoveListener(OnCancel);
                confirmButton.onClick.RemoveListener(OnConfirm);
                gameObject.SetActive(false);
            }

            void OnCancel() => tcs.TrySetResult(false);
            void OnConfirm() => tcs.TrySetResult(true);
        }

        // Plain-language confirmation copy for each recipient trust level. An unresolved gate falls back
        // to the most cautious (external wallet) phrasing.
        private static string BuildDescription(TransactionConfirmationRequest request)
        {
            string amount = Amount(request);

            switch (request.Gate)
            {
                case RecipientGate.Profile:
                    string name = HighlightLink(request.RecipientAddress, "@" + request.RecipientName);
                    return $"Are you sure you want to send {amount} to {name}?";
                case RecipientGate.SceneCreator:
                    string scene = Highlight(string.IsNullOrEmpty(request.RecipientName) ? "this scene" : request.RecipientName!);
                    return $"Are you sure you want to send {amount} to the creator of {scene}?";
                default:
                    return $"Are you sure you want to send {amount} to a wallet outside of Decentraland: {Highlight(request.RecipientAddress)}?";
            }
        }

        // "5 <MANA sprite>" for MANA, "5 ETH" for other assets, or "assets" when the transfer amount is unknown.
        private static string Amount(TransactionConfirmationRequest request)
        {
            if (string.IsNullOrEmpty(request.AmountDisplay) || string.IsNullOrEmpty(request.AssetSymbol))
                return "assets";

            string symbol = string.Equals(request.AssetSymbol, MANA_SYMBOL, StringComparison.OrdinalIgnoreCase)
                ? $"<sprite name=\"{MANA_SYMBOL}\">"
                : request.AssetSymbol!;

            return $"{request.AmountDisplay} {symbol}";
        }

        private static string Highlight(string? value) =>
            $"<color={HIGHLIGHT_COLOR}>{value}</color>";

        private static string HighlightLink(string? id, string label) =>
            $"<link=\"{id}\"><color={HIGHLIGHT_COLOR}><b>{label}</b></color></link>";
    }
}
