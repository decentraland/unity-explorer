using CodeLess.Attributes;
using System;
using System.Collections.Generic;

namespace DCL.CommunicationData.URLHelpers
{
    /// <summary>
    ///     Non-canonical URL comparer that compares the original string representation of the URLs.
    ///     In .NET Standard 2.0, the `Uri` class does not implement `IEqualityComparer{Uri}`
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class OriginalStringURLComparer : IEqualityComparer<Uri>
    {
        public bool Equals(Uri? x, Uri? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.IsAbsoluteUri == y.IsAbsoluteUri && x.OriginalString == y.OriginalString;
        }

        public int GetHashCode(Uri obj) =>
            HashCode.Combine(obj.IsAbsoluteUri, obj.OriginalString);
    }
}
