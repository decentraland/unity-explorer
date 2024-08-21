namespace DCL.CommandLine
{
    public interface ICommandLineArgs
    {
        public const string DEBUG_FLAG = "-debug";

        bool HasFlag(string flagName);
    }

    public static class CommandLineArgsExtensions
    {
        public static bool HasDebugFlag(this ICommandLineArgs args) =>
            args.HasFlag(ICommandLineArgs.DEBUG_FLAG);
    }
}
