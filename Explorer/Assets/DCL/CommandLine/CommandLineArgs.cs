using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.CommandLine
{
    public class CommandLineArgs : ICommandLineArgs
    {
        private readonly IReadOnlyList<string> parsedValues;

        public CommandLineArgs()
        {
            parsedValues = Environment.GetCommandLineArgs();
        }

        public bool HasFlag(string flagName) =>
            parsedValues.Contains(flagName);
    }
}
