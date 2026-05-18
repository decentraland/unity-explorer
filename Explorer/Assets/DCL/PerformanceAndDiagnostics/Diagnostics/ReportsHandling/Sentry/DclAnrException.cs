using System;

namespace DCL.Diagnostics.Sentry
{
    public class DclApplicationNotRespondingException : Exception
    {
        internal DclApplicationNotRespondingException() : base() { }
        internal DclApplicationNotRespondingException(string message) : base(message) { }
        internal DclApplicationNotRespondingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
