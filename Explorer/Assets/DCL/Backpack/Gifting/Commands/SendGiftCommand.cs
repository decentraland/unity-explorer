using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Presenters;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Views;
using MVC;

namespace DCL.Backpack.Gifting.Commands
{
    public class SendGiftCommand
    {
        private readonly IGiftingService giftingService;
        private readonly IMVCManager mvcManager;

        public SendGiftCommand(IGiftingService giftingService,
            IMVCManager mvcManager)
        {
            this.giftingService = giftingService;
            this.mvcManager = mvcManager;
        }

        public async UniTask<bool> ExecuteAsync(string recipientId, string? itemUrn)
        {
            if (string.IsNullOrEmpty(itemUrn))
            {
                return false;
            }

            try
            {
                return await giftingService.SendGiftAsync(recipientId, itemUrn, CancellationToken.None);
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}