using Cysharp.Threading.Tasks;
using DCL.Profiles;
using SuperScrollView;
using System;
using System.Threading;

namespace DCL.Friends.UI.Sections
{
    public abstract class FriendPanelRequestManager<T> : IDisposable where T : FriendPanelUserView
    {
        protected readonly IFriendsService friendsService;
        protected readonly IFriendsEventBus friendEventBus;
        protected readonly int pageSize;

        private readonly FriendPanelStatus firstCollectionStatus;
        private readonly FriendPanelStatus secondCollectionStatus;
        private readonly int statusElementIndex;
        private readonly int emptyElementIndex;
        private readonly int userElementIndex;

        private Action<T>? elementCustomizer;
        private Func<int, Profile> firstCollectionAccessor;
        private Func<int, Profile> secondCollectionAccessor;
        private int pageNumber = 0;
        private int totalFetched = 0;

        protected bool excludeFirstCollection = false;
        protected bool excludeSecondCollection = false;

        public bool HasElements { get; private set; }
        public bool WasInitialised { get; private set; }

        public event Action<Profile>? FriendElementClicked;
        public event Action? FirstFolderClicked;
        public event Action? SecondFolderClicked;

        protected FriendPanelRequestManager(IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            int pageSize,
            FriendPanelStatus firstCollectionStatus,
            FriendPanelStatus secondCollectionStatus,
            int statusElementIndex,
            int emptyElementIndex,
            int userElementIndex)
        {
            this.friendsService = friendsService;
            this.friendEventBus = friendEventBus;
            this.pageSize = pageSize;
            this.firstCollectionStatus = firstCollectionStatus;
            this.secondCollectionStatus = secondCollectionStatus;
            this.statusElementIndex = statusElementIndex;
            this.emptyElementIndex = emptyElementIndex;
            this.userElementIndex = userElementIndex;
        }

        protected void SetElementCustomizer(Action<T> customizer) { elementCustomizer = customizer; }

        protected void ConfigureAccessors(Func<int, Profile> firstAccessor,
            Func<int, Profile> secondAccessor)
        {
            this.firstCollectionAccessor = firstAccessor;
            this.secondCollectionAccessor = secondAccessor;
        }

        public abstract void Dispose();

        protected abstract int GetFirstCollectionCount();
        protected abstract int GetSecondCollectionCount();

        public LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = null;
            int onlineFriendMarker = excludeFirstCollection ? 0 : GetFirstCollectionCount();
            if (GetFirstCollectionCount() == 0) onlineFriendMarker++; //Count the empty element
            int offlineFriendMarker = excludeSecondCollection ? 0 : GetSecondCollectionCount();
            if (GetSecondCollectionCount() == 0) offlineFriendMarker++; //Count the empty element

            if (index == 0)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[statusElementIndex].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(firstCollectionStatus, GetFirstCollectionCount());
                statusWrapperView.ResetCallback();
                statusWrapperView.FolderButtonClicked += FolderClick;
            }
            else if (index > 0 && index <= onlineFriendMarker)
            {
                if (GetFirstCollectionCount() == 0)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[emptyElementIndex].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[userElementIndex].mItemPrefab.name);
                    T friendListUserView = listItem.GetComponent<T>();
                    friendListUserView.Configure(firstCollectionAccessor(index - 1));
                    elementCustomizer?.Invoke(friendListUserView);
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => FriendElementClicked?.Invoke(profile);
                }
            }
            else if (index == onlineFriendMarker + 1)
            {
                listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[statusElementIndex].mItemPrefab.name);
                StatusWrapperView statusWrapperView = listItem.GetComponent<StatusWrapperView>();
                statusWrapperView.SetStatusText(secondCollectionStatus, GetSecondCollectionCount());
                statusWrapperView.ResetCallback();
                statusWrapperView.FolderButtonClicked += FolderClick;
            }
            else if (index > onlineFriendMarker + 1 && index <= onlineFriendMarker + 1 + offlineFriendMarker + 1)
            {
                if (GetSecondCollectionCount() == 0)
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[emptyElementIndex].mItemPrefab.name);
                else
                {
                    listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[userElementIndex].mItemPrefab.name);
                    T friendListUserView = listItem.GetComponent<T>();
                    friendListUserView.Configure(secondCollectionAccessor(index - onlineFriendMarker - 2));
                    elementCustomizer?.Invoke(friendListUserView);
                    friendListUserView.RemoveMainButtonClickListeners();
                    friendListUserView.MainButtonClicked += profile => FriendElementClicked?.Invoke(profile);
                }
            }

            return listItem;
        }

        public int GetElementsNumber()
        {
            int count = 2;

            if (!excludeFirstCollection)
                count += GetFirstCollectionCount();

            if (!excludeSecondCollection)
                count += GetSecondCollectionCount();

            if (GetFirstCollectionCount() == 0)
                count++;

            if (GetSecondCollectionCount() == 0)
                count++;

            return count;
        }

        public void Reset()
        {
            HasElements = false;
            WasInitialised = false;
            pageNumber = 0;
            totalFetched = 0;
        }

        protected abstract UniTask FetchInitialData(CancellationToken ct);

        public async UniTask Init(CancellationToken ct)
        {
            await FetchInitialData(ct);

            HasElements = GetFirstCollectionCount() + GetSecondCollectionCount() > 0;
            WasInitialised = true;
        }

        private void FolderClick(bool isFolded, FriendPanelStatus panelStatus)
        {
            if (panelStatus == firstCollectionStatus)
            {
                excludeFirstCollection = isFolded;
                FirstFolderClicked?.Invoke();
            }
            else if (panelStatus == secondCollectionStatus)
            {
                excludeSecondCollection = isFolded;
                SecondFolderClicked?.Invoke();
            }
        }
    }
}
