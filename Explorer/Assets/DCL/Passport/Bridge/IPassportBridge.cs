using Cysharp.Threading.Tasks;
using DCL.Web3;

namespace DCL.Passport
{
    // We need the interface as the implementation must be in a different assembly, otherwise we get cyclic dependencies
    public interface IPassportBridge
    {
        public UniTask ShowAsync(string userId) =>
            ShowAsync(new PassportParams(userId));

        UniTask ShowAsync(PassportParams passportParams);
    }
}
