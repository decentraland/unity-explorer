using System.Threading;

namespace DCL.Diagnostics
{
    public static class CurrentLoadingStage
    {
        public const string UNKNOWN = "Unknown";

        private static string current = UNKNOWN;

        public static string Value => Volatile.Read(ref current);

        public static void Set(string stage)
        {
            Volatile.Write(ref current, stage);
        }
    }
}