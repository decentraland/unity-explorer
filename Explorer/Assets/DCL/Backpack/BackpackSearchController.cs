using Cysharp.Threading.Tasks;
using DCL.Backpack.BackpackBus;
using DCL.Input.Component;
using DCL.Input.UnityInputSystem.Blocks;
using DCL.UI;
using System.Threading;
using Utility;

namespace DCL.Backpack
{
    public class BackpackSearchController
    {
        private const int SEARCH_AWAIT_TIME = 1000;
        private readonly SearchBarView view;
        private readonly IBackpackCommandBus commandBus;
        private readonly IInputBlock inputBlock;

        private CancellationTokenSource? searchCancellationToken;

        public BackpackSearchController(SearchBarView view,
            IBackpackCommandBus commandBus,
            IBackpackEventBus backpackEventBus,
            IInputBlock inputBlock)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.inputBlock = inputBlock;

            backpackEventBus.SearchEvent += OnSearchEvent;

            view.inputField.onSelect.AddListener(DisableShortcutsInput);
            view.inputField.onDeselect.AddListener(RestoreInput);
            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);
        }

        private void RestoreInput(string text)
        {
            inputBlock.UnblockInputs(InputMapComponent.Kind.Shortcuts);
        }

        private void DisableShortcutsInput(string text)
        {
            inputBlock.BlockInputs(InputMapComponent.Kind.Shortcuts);
        }

        private void OnSearchEvent(string searchString)
        {
            if(string.IsNullOrEmpty(searchString))
                ClearSearch();
        }

        private void ClearSearch()
        {
            view.inputField.text = string.Empty;
            view.clearSearchButton.gameObject.SetActive(false);
        }

        private void OnValueChanged(string searchText)
        {
            view.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(searchText));

            searchCancellationToken = searchCancellationToken.SafeRestart();
            AwaitAndSendSearchAsync(searchText, searchCancellationToken.Token).Forget();
        }

        private async UniTaskVoid AwaitAndSendSearchAsync(string searchText, CancellationToken ct)
        {
            await UniTask.Delay(SEARCH_AWAIT_TIME, cancellationToken: ct);
            commandBus.SendCommand(new BackpackSearchCommand(searchText));
        }
    }
}
