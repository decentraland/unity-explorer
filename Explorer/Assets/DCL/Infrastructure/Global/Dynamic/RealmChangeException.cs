using System;

namespace DCL.Global.Dynamic
{
    public class RealmChangeException : Exception
    {
        public RealmChangeException(string message, Exception innerException) : base(message, innerException) { }
    }
}
