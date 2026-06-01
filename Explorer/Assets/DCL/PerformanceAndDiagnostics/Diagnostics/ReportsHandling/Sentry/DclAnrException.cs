using System;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
#if UNITY_STANDALONE_WIN
        public readonly string? DumpFilePath;

        internal DclApplicationNotRespondingException(string message, string? dumpFilePath) : base(message)
        {
            this.DumpFilePath = dumpFilePath;
        }
#else
        internal DclApplicationNotRespondingException(string message) : base(message) { }
#endif
    }
}
