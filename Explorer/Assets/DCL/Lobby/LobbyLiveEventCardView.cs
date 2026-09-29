using TMPro;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Hero card of a live event: full-bleed thumbnail, name and host, plus how many people are attending right now.
    /// </summary>
    public class LobbyLiveEventCardView : LobbyCardView
    {
        [field: SerializeField]
        public TMP_Text HostText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject AttendeesGroup { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text AttendeesText { get; private set; } = null!;
    }
}
