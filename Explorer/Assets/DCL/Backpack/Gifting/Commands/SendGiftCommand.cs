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
        private readonly IGiftTransferService _giftTransferService;
        private readonly IMVCManager mvcManager;

        public SendGiftCommand(IGiftTransferService giftTransferService,
            IMVCManager mvcManager)
        {
            _giftTransferService = giftTransferService;
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
                // return await _giftTransferService.SendGiftAsync(recipientId, itemUrn, CancellationToken.None);
                return false;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}