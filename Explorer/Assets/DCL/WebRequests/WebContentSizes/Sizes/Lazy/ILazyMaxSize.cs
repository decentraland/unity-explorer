using Cysharp.Threading.Tasks;

namespace DCL.WebRequests.WebContentSizes.Sizes.Lazy
{
    public interface ILazyMaxSize : IMaxSize
    {
        void Initialize(IMaxSize maxSize);
    }
}
