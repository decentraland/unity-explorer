using Cysharp.Threading.Tasks;
using System;

namespace DCL.WebRequests.WebContentSizes.Sizes.Lazy
{
    public interface ILazyMaxSize : IMaxSize
    {
        void Initialize(IMaxSize maxSize);
    }
}
