using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Pill of the friends at a place: up to <see cref="MAX_SLOTS" /> pictures and a "+N" label for the rest, styled by LobbyConnectedFriends.uss.
    /// </summary>
    [UxmlElement]
    public partial class LobbyConnectedFriendsElement : VisualElement
    {
        public const int MAX_SLOTS = 3;

        private const string USS_BLOCK = "lobby-connected-friends";
        private const string USS_SHOWN = USS_BLOCK + "--shown";
        private const string USS_WITH_OVERFLOW = USS_BLOCK + "--with-overflow";
        private const string USS_PICTURE = USS_BLOCK + "__picture";
        private const string USS_PICTURE_LOADING = USS_PICTURE + "--loading";
        private const string USS_OVERFLOW = USS_BLOCK + "__overflow";

        private readonly VisualElement[] slots = new VisualElement[MAX_SLOTS];
        private readonly Label overflow;

        private int count;

        [UxmlAttribute]
        public int Count
        {
            get => count;

            set
            {
                count = value;
                EnableInClassList(USS_SHOWN, value > 0);
                EnableInClassList(USS_WITH_OVERFLOW, value > MAX_SLOTS);
                overflow.text = value > MAX_SLOTS ? $"+{value - MAX_SLOTS}" : string.Empty;

                for (var i = 0; i < MAX_SLOTS; i++)
                    slots[i].SetDisplayed(i < value);
            }
        }

        public LobbyConnectedFriendsElement()
        {
            AddToClassList(USS_BLOCK);
            pickingMode = PickingMode.Ignore;

            // Picked for the hover tooltip; without a manipulator a click on a slot still reaches the card
            for (var i = 0; i < MAX_SLOTS; i++)
            {
                var slot = new VisualElement { pickingMode = PickingMode.Position };
                slot.AddToClassList(USS_PICTURE);
                slot.SetDisplayed(false);
                slots[i] = slot;
                Add(slot);
            }

            overflow = new Label { pickingMode = PickingMode.Ignore };
            overflow.AddToClassList(USS_OVERFLOW);
            Add(overflow);
        }

        public VisualElement Slot(int index) =>
            slots[index];

        public void SetPicture(int index, Sprite? sprite, Color color, bool loading)
        {
            VisualElement slot = slots[index];
            slot.style.backgroundColor = color;
            slot.style.backgroundImage = LobbyCardBackground.From(sprite);
            slot.EnableInClassList(USS_PICTURE_LOADING, loading);
        }
    }
}
