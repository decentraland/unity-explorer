using Cysharp.Threading.Tasks;
using DCL.Profiles;
using SuperScrollView;
using System;
using System.Threading;

namespace DCL.Friends.UI.FriendPanel.Sections
{
    public abstract class FriendPanelRequestManager<T> : IDisposable where T : FriendPanelUserView
    {
        protected readonly int pageSize;

        private int pageNumber = 0;
        private int totalFetched = 0;

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        public event Action<Profile>? ElementClicked;

        public FriendPanelRequestManager(int pageSize)
        {
            this.pageSize = pageSize;
        }

        public abstract void Dispose();

        public abstract int GetCollectionCount();
        protected abstract Profile GetCollectionElement(int index);

        protected virtual void CustomiseElement(T elementView, int index) { }

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            T view = listItem.GetComponent<T>();
            view.Configure(GetCollectionElement(index));

            view.RemoveMainButtonClickListeners();
            view.MainButtonClicked += profile => ElementClicked?.Invoke(profile);

            CustomiseElement(view, index);

            return listItem;
        }

        protected abstract UniTask FetchInitialData(CancellationToken ct);

        public async UniTask Init(CancellationToken ct)
        {
            await FetchInitialData(ct);

            HasElements = GetCollectionCount() > 0;
            WasInitialised = true;
        }

        public virtual void Reset()
        {
            HasElements = false;
            WasInitialised = false;
            pageNumber = 0;
            totalFetched = 0;
        }
    }
}
