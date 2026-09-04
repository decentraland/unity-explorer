using DCL.Multiplayer.Connections.Rooms;
using System.Text;
using Utility;

namespace DCL.Multiplayer.Connections.Systems.RoomIndicator
{
    /// <summary>
    ///     Builds the debug label drawn above a remote avatar's nametag.
    ///     <para>
    ///         Every room the avatar is known through is prefixed with a glyph telling apart the two independent facts
    ///         the indicator reports: whether LiveKit's participant roster lists the wallet in that room, and whether
    ///         that room's data channel delivered an <c>AnnounceProfileVersion</c> for it. The two diverge because a
    ///         peer carried by Pulse joins the LiveKit rooms without ever announcing over them.
    ///     </para>
    ///     <para>Pulse exposes no roster to read, so its announcement is the only signal it can report.</para>
    ///     <para>
    ///         Glyphs are written as escapes to keep this file ASCII. Every codepoint used here must exist in the
    ///         sprite asset the nametags panel resolves text against, or it renders as a missing glyph.
    ///     </para>
    /// </summary>
    public static class RoomIndicatorLabel
    {
        /// <summary>Green circle: a LiveKit participant that also announced over the room, the full compatibility path.</summary>
        public const string PRESENT_AND_ANNOUNCED = "\U0001F7E2";

        /// <summary>Link: a LiveKit participant that never announced over the room, the steady state under Pulse.</summary>
        public const string PRESENT_ONLY = "\U0001F517";

        /// <summary>Ghost: an announcement from a wallet the roster no longer lists, a stale entry or a hand-off in flight.</summary>
        public const string ANNOUNCED_ONLY = "\U0001F47B";

        /// <summary>High voltage: announced over Pulse.</summary>
        public const string PULSE = "\u26A1";

        /// <summary>Drawn when no room accounts for the avatar at all.</summary>
        public const string NONE = "None";

        /// <summary>Reused across builds; the indicator is written from the main thread only.</summary>
        private static readonly StringBuilder BUILDER = new (48);

        /// <summary>The label for one avatar, or <see cref="NONE" /> when no room accounts for it.</summary>
        /// <param name="announced">Rooms that delivered an announcement, as recorded on the participant table.</param>
        /// <param name="present">LiveKit rooms whose roster lists the wallet.</param>
        public static string Build(RoomSource announced, RoomSource present)
        {
            BUILDER.Clear();

            AppendLiveKitRoom(RoomSource.Gatekeeper, nameof(RoomSource.Gatekeeper), announced, present);
            AppendLiveKitRoom(RoomSource.Island, nameof(RoomSource.Island), announced, present);

            if (EnumUtils.HasFlag(announced, RoomSource.Pulse))
                Append(PULSE, nameof(RoomSource.Pulse));

            return BUILDER.Length == 0 ? NONE : BUILDER.ToString();
        }

        private static void AppendLiveKitRoom(RoomSource room, string name, RoomSource announced, RoomSource present)
        {
            bool isPresent = EnumUtils.HasFlag(present, room);
            bool isAnnounced = EnumUtils.HasFlag(announced, room);

            if (!isPresent && !isAnnounced)
                return;

            if (!isPresent)
                Append(ANNOUNCED_ONLY, name);
            else if (isAnnounced)
                Append(PRESENT_AND_ANNOUNCED, name);
            else
                Append(PRESENT_ONLY, name);
        }

        private static void Append(string glyph, string name)
        {
            if (BUILDER.Length > 0)
                BUILDER.Append(' ');

            BUILDER.Append(glyph).Append(name);
        }
    }
}
