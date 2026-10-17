using System;

namespace DCL.Utility.Exceptions
{
    public class ManifestNotFoundException : Exception
    {
        public ManifestNotFoundException(string message) : base(message) { }
    }
}
