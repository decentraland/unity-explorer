using Cysharp.Threading.Tasks;
using DCL.Passport;
using DCL.Web3;
using MVC;

namespace DCL.Friends.Passport
{
    public class MVCPassportBridge : IPassportBridge
    {
        private readonly IMVCManager mvcManager;

        public MVCPassportBridge(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public async UniTask ShowAsync(Web3Address userId)
        {
            await mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(userId.ToString())));
        }
    }
}
