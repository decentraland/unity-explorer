namespace DCL.Diagnostics
{
    public static class CurrentLoadingStage
    {
        public const string UNKNOWN = "Unknown";

        public static string Value { get; private set; } = UNKNOWN;

        public static void Set(string stage)
        {
            Value = stage;
        }
    }
}
