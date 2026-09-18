using TMPro;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Compact event card of the lobby: thumbnail, name and host, plus a live badge with the attendees count
    ///     and a schedule line for the events yet to start.
    /// </summary>
    public class LobbyEventCardView : LobbyCardView
    {
        [field: SerializeField]
        public TMP_Text HostText { get; private set; } = null!;

        [field: Header("Live")]
        [field: SerializeField]
        public GameObject LiveBadge { get; private set; } = null!;

        [field: SerializeField]
        public GameObject AttendeesGroup { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text AttendeesText { get; private set; } = null!;

        [field: Header("Upcoming")]
        [field: SerializeField]
        public TMP_Text ScheduleText { get; private set; } = null!;
    }
}
