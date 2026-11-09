using DCL.Friends;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Fills the friends rows of the lobby cards with the online friends among a place's connected addresses, keeps
    ///     them current with the connectivity tracker and names the hovered picture in a shared tooltip.
    /// </summary>
    public class LobbyConnectedFriendsPresenter : IDisposable
    {
        // Panel pixels from the pointer to the left edge of the tooltip
        private static readonly Vector2 TOOLTIP_OFFSET = new (16f, 0f);

        private readonly FriendsConnectivityStatusTracker tracker;

        // The server does not guarantee the casing of the addresses
        private readonly Dictionary<string, Profile.CompactInfo> onlineFriends = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<Profile.CompactInfo> friendsBuffer = new ();
        private readonly List<Profile.CompactInfo> matched = new ();
        private readonly Dictionary<LobbyConnectedFriendsElement, CardBinding> bindings = new ();

        private VisualElement? tooltip;
        private Label? tooltipLabel;
        private SlotBinding? hoveredSlot;
        private CancellationToken showCt;

        public LobbyConnectedFriendsPresenter(FriendsConnectivityStatusTracker tracker)
        {
            this.tracker = tracker;
        }

        public void Dispose()
        {
            Hide();

            foreach (CardBinding binding in bindings.Values)
                binding.Dispose();

            bindings.Clear();
        }

        public void Show(VisualElement nameTooltip, CancellationToken ct)
        {
            showCt = ct;
            tooltip = nameTooltip;
            tooltipLabel = nameTooltip.Q<Label>();
            nameTooltip.SetDisplayed(false);

            tracker.OnFriendBecameOnline += OnFriendStatusChanged;
            tracker.OnFriendBecameAway += OnFriendStatusChanged;
            tracker.OnFriendBecameOffline += OnFriendStatusChanged;
            tracker.OnFriendRemoved += OnFriendRemoved;
            tracker.OnReset += Refresh;

            Refresh();
        }

        public void Hide()
        {
            tracker.OnFriendBecameOnline -= OnFriendStatusChanged;
            tracker.OnFriendBecameAway -= OnFriendStatusChanged;
            tracker.OnFriendBecameOffline -= OnFriendStatusChanged;
            tracker.OnFriendRemoved -= OnFriendRemoved;
            tracker.OnReset -= Refresh;

            HideTooltip();
            tooltip = null;
            tooltipLabel = null;
        }

        /// <summary>
        ///     The row is kept so the next tracker change refreshes it.
        /// </summary>
        public void Bind(LobbyConnectedFriendsElement friends, string[]? addresses)
        {
            if (!bindings.TryGetValue(friends, out CardBinding binding))
            {
                binding = new CardBinding(friends, this);
                bindings.Add(friends, binding);
            }

            binding.Addresses = addresses;
            Apply(binding);
        }

        private void OnFriendStatusChanged(Profile.CompactInfo _) =>
            Refresh();

        private void OnFriendRemoved(string _) =>
            Refresh();

        private void Refresh()
        {
            onlineFriends.Clear();
            friendsBuffer.Clear();
            tracker.CopyOnlineFriendsTo(friendsBuffer);

            foreach (Profile.CompactInfo friend in friendsBuffer)
                onlineFriends[friend.UserId.Value] = friend;

            foreach (CardBinding binding in bindings.Values)
                Apply(binding);
        }

        private void Apply(CardBinding binding)
        {
            matched.Clear();

            if (binding.Addresses != null)
                foreach (string address in binding.Addresses)
                    if (onlineFriends.TryGetValue(address, out Profile.CompactInfo friend))
                        matched.Add(friend);

            binding.Element.Count = matched.Count;

            for (var i = 0; i < LobbyConnectedFriendsElement.MAX_SLOTS; i++)
            {
                if (i < matched.Count)
                    binding.Slots[i].Show(matched[i], showCt);
                else
                    binding.Slots[i].Clear();
            }

            if (hoveredSlot == null || !ReferenceEquals(hoveredSlot.Owner, binding)) return;

            // The hovered picture may now show somebody else, or nobody
            if (hoveredSlot.Index < matched.Count)
                NameTooltip(hoveredSlot.Name);
            else
                HideTooltip();
        }

        private void ShowTooltip(SlotBinding slot, Vector2 panelPosition)
        {
            hoveredSlot = slot;

            if (tooltip == null) return;

            NameTooltip(slot.Name);
            tooltip.MoveToPointer(panelPosition, TOOLTIP_OFFSET);
            tooltip.SetDisplayed(true);
        }

        private void MoveTooltip(SlotBinding slot, Vector2 panelPosition)
        {
            if (!ReferenceEquals(hoveredSlot, slot)) return;

            tooltip?.MoveToPointer(panelPosition, TOOLTIP_OFFSET);
        }

        private void HideTooltip(SlotBinding slot)
        {
            if (ReferenceEquals(hoveredSlot, slot))
                HideTooltip();
        }

        private void HideTooltip()
        {
            hoveredSlot = null;
            tooltip?.SetDisplayed(false);
        }

        private void NameTooltip(string name)
        {
            if (tooltipLabel != null)
                tooltipLabel.text = name;
        }

        private class CardBinding : IDisposable
        {
            public readonly LobbyConnectedFriendsElement Element;
            public readonly SlotBinding[] Slots = new SlotBinding[LobbyConnectedFriendsElement.MAX_SLOTS];

            public string[]? Addresses;

            public CardBinding(LobbyConnectedFriendsElement element, LobbyConnectedFriendsPresenter presenter)
            {
                Element = element;

                for (var i = 0; i < Slots.Length; i++)
                    Slots[i] = new SlotBinding(this, i, presenter);
            }

            public void Dispose()
            {
                foreach (SlotBinding slot in Slots)
                    slot.Dispose();
            }
        }

        private class SlotBinding : IDisposable
        {
            private readonly LobbyConnectedFriendsPresenter presenter;
            private readonly VisualElement slot;
            private readonly LobbyProfilePictureBinding picture;

            public CardBinding Owner { get; }

            public int Index { get; }

            public string Name { get; private set; } = string.Empty;

            public SlotBinding(CardBinding owner, int index, LobbyConnectedFriendsPresenter presenter)
            {
                Owner = owner;
                Index = index;
                this.presenter = presenter;
                slot = owner.Element.Slot(index);
                picture = new LobbyProfilePictureBinding(OnThumbnailUpdated);

                slot.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
                slot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                slot.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            }

            public void Dispose()
            {
                picture.Dispose();
                slot.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                slot.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                slot.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            }

            public void Show(in Profile.CompactInfo profile, CancellationToken ct)
            {
                Name = profile.Name;
                picture.Load(profile, ct);
            }

            public void Clear()
            {
                Name = string.Empty;
                picture.Clear();
            }

            private void OnThumbnailUpdated(ProfileThumbnailViewModel model) =>
                Owner.Element.SetPicture(Index, model.Sprite, model.ProfileColor, model.ThumbnailState == ProfileThumbnailViewModel.State.Loading && model.Sprite == null);

            private void OnPointerEnter(PointerEnterEvent evt) =>
                presenter.ShowTooltip(this, evt.position);

            private void OnPointerMove(PointerMoveEvent evt) =>
                presenter.MoveTooltip(this, evt.position);

            private void OnPointerLeave(PointerLeaveEvent evt) =>
                presenter.HideTooltip(this);
        }
    }
}
