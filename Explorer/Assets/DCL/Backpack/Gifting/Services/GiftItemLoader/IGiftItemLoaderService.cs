using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Models;

namespace DCL.Backpack.Gifting.Services.GiftItemLoader
{
    public interface IGiftItemLoaderService
    {
        /// <summary>
        /// Fetches the JSON metadata from the Lambda and returns the domain model.
        /// </summary>
        UniTask<GiftItemModel?> LoadItemMetadataAsync(string tokenUri, CancellationToken ct);
    }
}