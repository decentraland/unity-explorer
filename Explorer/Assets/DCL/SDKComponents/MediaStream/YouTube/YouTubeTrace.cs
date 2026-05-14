using DCL.Diagnostics;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    // Temporary timing instrumentation for issue #8350 (Admin Tool YouTube share 10-30s delay).
    // Tagged "[8350]" so logs can be grepped from a Player log and the entire helper removed
    // once the dominant span has been identified.
    internal static class YouTubeTrace
    {
        internal static void Log(string evt) =>
            ReportHub.LogProductionInfo($"[8350-T{UnityEngine.Time.realtimeSinceStartup:F3}] {evt}");
    }
}
