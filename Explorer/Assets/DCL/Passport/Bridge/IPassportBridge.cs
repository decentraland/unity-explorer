using Cysharp.Threading.Tasks;

namespace DCL.Passport
{
    // We need the interface as the implementation must be in a different assembly, otherwise we get cyclic dependencies
    public interface IPassportBridge
    {
        UniTask ShowAsync(string userId) =>
            ShowAsync(new PassportParams(userId));

        UniTask ShowAsync(PassportParams passportParams);
    }
}
