using DCL.Diagnostics;
using LiveKit.Internal.FFIClients;

namespace DCL.Multiplayer.Connections.FfiClients
{
    public static class FfiClientExtensions
    {
        public static void EnsureInitialize(this IFFIClient ffiClient)
        {
            bool initialized = ffiClient.Initialized();
            ReportHub.Log(ReportData.UNSPECIFIED, $"FfiClient initilized: {initialized}");

            if (initialized == false)
            {
                ReportHub.Log(ReportData.UNSPECIFIED, "FfiClient initialize start");
                ffiClient.Initialize();
                ReportHub.Log(ReportData.UNSPECIFIED, "FfiClient initialize finish");
            }
        }
    }
}
