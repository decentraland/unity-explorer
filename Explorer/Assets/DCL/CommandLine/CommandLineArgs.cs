using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.CommandLine
{
    public class CommandLineArgs : ICommandLineArgs
    {
        private readonly IReadOnlyList<string> parsedValues;
        private static readonly HashSet<string> ALWAYS_IN_EDITOR = new ()
        {
            ICommandLineArgs.DEBUG_FLAG
        };

        public CommandLineArgs()
        {
            parsedValues = Environment.GetCommandLineArgs();

            if (Application.isEditor)
                parsedValues = parsedValues.Union(ALWAYS_IN_EDITOR).ToList();
        }

        public bool HasFlag(string flagName) =>
            parsedValues.Contains(flagName);
    }
}
