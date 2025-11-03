using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Backpack.Gifting.Services
{
    public interface IGiftingService
    {
        UniTask<bool> SendGiftAsync(string userId, string urn, CancellationToken none);
    }

    public class GiftingService : IGiftingService
    {
        public UniTask<bool> SendGiftAsync(string userId, string urn, CancellationToken none)
        {
            throw new NotImplementedException();
        }
    }
}