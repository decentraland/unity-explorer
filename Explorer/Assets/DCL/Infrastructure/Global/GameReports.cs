using DCL.Diagnostics;
using Global.AppArgs;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Global
{
    public static class GameReports
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PrintIsDead(IAppArgs appArgs)
        {
            ReportHub.LogError(ReportCategory.ENGINE,
                "Initialization Failed! Game is irrecoverably dead!");

            if (appArgs.HasFlag(AppArgsFlags.AUTOPILOT))
                Application.Quit(1);
        }
    }
}
