using Cysharp.Threading.Tasks;
using DCL.Backpack.BackpackBus;
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
        private readonly DCLInput dclInput;

        private CancellationTokenSource? searchCancellationToken;

        public BackpackSearchController(SearchBarView view,
            IBackpackCommandBus commandBus,
            IBackpackEventBus backpackEventBus,
            DCLInput dclInput)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.dclInput = dclInput;

            backpackEventBus.SearchEvent += OnSearchEvent;

            view.inputField.onSelect.AddListener(DisableShortcutsInput);
            view.inputField.onDeselect.AddListener(RestoreInput);
            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);
        }

        private void RestoreInput(string text) =>
            dclInput.Shortcuts.Enable();

        private void DisableShortcutsInput(string text) =>
            dclInput.Shortcuts.Disable();

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
