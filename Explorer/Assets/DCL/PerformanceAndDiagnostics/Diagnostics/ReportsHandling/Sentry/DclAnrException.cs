using System;
using System.Collections.Generic;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
#if UNITY_STANDALONE_WIN
        public readonly IReadOnlyList<string> DumpFilePaths;

        internal DclApplicationNotRespondingException(string message, IReadOnlyList<string> dumpFilePaths) : base(message)
        {
            this.DumpFilePaths = dumpFilePaths;
        }
#else
        internal DclApplicationNotRespondingException(string message) : base(message) { }
#endif
    }
}
