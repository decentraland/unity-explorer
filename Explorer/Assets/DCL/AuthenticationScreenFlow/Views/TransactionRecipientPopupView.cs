using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    ///     Second confirmation step of the Web3 confirmation popup, stating in plain language who receives
    ///     the assets. Lives as its own prefab nested inside the Web3 confirmation popup.
    /// </summary>
    public class TransactionRecipientPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        public async UniTask<bool> ShowAsync(string descriptionText, CancellationToken ct)
        {
            description.text = descriptionText;
            gameObject.SetActive(true);

            try
            {
                int clickedIndex = await UniTask.WhenAny(confirmButton.OnClickAsync(ct), cancelButton.OnClickAsync(ct));
                return clickedIndex == 0;
            }
            finally { gameObject.SetActive(false); }
        }
    }
}
