using Cysharp.Threading.Tasks;
using System;

namespace DCL.WebRequests.WebContentSizes.Sizes.Lazy
{
    [Obsolete("Obsolete since implemented custom compression")]
    public interface ILazyMaxSize : IMaxSize
    {
        void Initialize(IMaxSize maxSize);
    }
}
