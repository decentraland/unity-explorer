using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Services;

namespace DCL.Backpack.Gifting.Commands
{
    public class SendGiftCommand
    {
        private readonly IGiftingService giftingService;

        public SendGiftCommand(IGiftingService giftingService)
        {
            this.giftingService = giftingService;
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