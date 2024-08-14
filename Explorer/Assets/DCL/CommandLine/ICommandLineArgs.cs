namespace DCL.CommandLine
{
    public interface ICommandLineArgs
    {
        bool HasFlag(string flagName);
    }

    public static class CommandLineArgsExtensions
    {
        public static bool HasDebugFlag(this ICommandLineArgs args) =>
            args.HasFlag("-debug");
    }
}
