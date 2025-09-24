using Cysharp.Threading.Tasks;
using DCL.Passport;
using DCL.Web3;
using MVC;
using System;

namespace DCL.Friends.Passport
{
    public class MVCPassportBridge : IPassportBridge
    {
        private readonly IMVCManager mvcManager;

        public MVCPassportBridge(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public UniTask ShowAsync(PassportParams passportParams) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(passportParams));
    }
}
