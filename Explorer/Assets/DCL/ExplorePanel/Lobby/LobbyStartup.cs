using Global.AppArgs;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Preserves destination launches while making Home the normal entry point.</summary>
    public static class LobbyStartup
    {
        public static bool ShouldOpen(IAppArgs args, bool enabled)
        {
            if (!enabled) return false;
            if (args.HasFlag(AppArgsFlags.DISABLE_HUD) || args.HasFlag(AppArgsFlags.FORCE_OPEN_BACKPACK)) return false;
            if (args.HasFlag(AppArgsFlags.OPEN_LOBBY)) return !args.HasFlagWithValueFalse(AppArgsFlags.OPEN_LOBBY);
            return !args.HasFlag(AppArgsFlags.POSITION)
                   && !args.HasFlag(AppArgsFlags.REALM)
                   && !args.HasFlag(AppArgsFlags.LOCAL_SCENE)
                   && !args.HasFlag(AppArgsFlags.COMMUNITY);
        }
    }
}
