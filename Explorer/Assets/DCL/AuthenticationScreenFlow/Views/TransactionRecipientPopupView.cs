using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    ///     Second confirmation step of the Web3 confirmation popup, naming who receives the assets.
    /// </summary>
    public class TransactionRecipientPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text description = null!;
        [SerializeField] private Button cancelButton = null!;
        [SerializeField] private Button confirmButton = null!;

        public async UniTask<bool> ShowForResultAsync(string descriptionText, CancellationToken ct)
        {
            description.text = descriptionText;
            gameObject.SetActive(true);

            // The button that was not clicked stays subscribed until its token is cancelled.
            using var clickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                int clickedIndex = await UniTask.WhenAny(confirmButton.OnClickAsync(clickCts.Token), cancelButton.OnClickAsync(clickCts.Token));
                return clickedIndex == 0;
            }
            finally
            {
                clickCts.Cancel();
                gameObject.SetActive(false);
            }
        }
    }
}
