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

        private CancellationTokenSource cts;

        public BackpackSearchController(SearchBarView view, IBackpackCommandBus commandBus, IBackpackEventBus backpackEventBus)
        {
            this.view = view;
            this.commandBus = commandBus;

            backpackEventBus.SearchEvent += OnSearchEvent;

            view.inputField.onValueChanged.AddListener(OnValueChanged);
            view.clearSearchButton.onClick.AddListener(ClearSearch);
            view.clearSearchButton.gameObject.SetActive(false);
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

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            AwaitAndSendSearchAsync(searchText).Forget();
        }

        private async UniTaskVoid AwaitAndSendSearchAsync(string searchText)
        {
            await UniTask.Delay(SEARCH_AWAIT_TIME, cancellationToken: cts.Token);
            commandBus.SendCommand(new BackpackSearchCommand(searchText));
        }
    }
}
