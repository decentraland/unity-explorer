using System;

namespace Utility.CodeConventions
{
    public class IgnoreAsyncNamingAttribute : Attribute
    {
        public string Reason { get; }

        public IgnoreAsyncNamingAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
