using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     What every card of a <see cref="LobbyCarouselView" /> has: a thumbnail, a title and a button covering the whole card.
    ///     What else it displays is up to the subclass and to the owner filling it.
    /// </summary>
    public abstract class LobbyCardView : MonoBehaviour
    {
        [field: SerializeField]
        public Button Button { get; private set; } = null!;

        [field: SerializeField]
        public ImageView Thumbnail { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; } = null!;

        [SerializeField] private ButtonView? jumpInButton;
        [SerializeField] private Sprite? defaultThumbnail;

        /// <summary>
        ///     Left unassigned on cards that cannot jump in on their own.
        /// </summary>
        public ButtonView? JumpInButton => jumpInButton;

        public Sprite? DefaultThumbnail => defaultThumbnail;
    }
}
