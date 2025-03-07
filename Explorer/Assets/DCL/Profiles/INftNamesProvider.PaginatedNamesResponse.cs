using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    public partial interface INftNamesProvider
    {
        public readonly struct PaginatedNamesResponse : IDisposable
        {
            private readonly List<string> names;

            public readonly int TotalAmount;
            public IReadOnlyList<string> Names => names;

            public PaginatedNamesResponse(int totalAmount, IEnumerable<string> names)
            {
                this.names = ListPool<string>.Get();
                this.names.AddRange(names);
                TotalAmount = totalAmount;
            }

            public void Dispose()
            {
                ListPool<string>.Release(names);
            }
        }
    }
}
