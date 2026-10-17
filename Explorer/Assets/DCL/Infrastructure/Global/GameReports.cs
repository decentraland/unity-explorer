using DCL.Diagnostics;
using System.Runtime.CompilerServices;

namespace Global
{
    public static class GameReports
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PrintIsDead()
        {
            ReportHub.LogError(ReportCategory.ENGINE, "Initialization Failed! Game is irrecoverably dead!");
        }
    }
}
