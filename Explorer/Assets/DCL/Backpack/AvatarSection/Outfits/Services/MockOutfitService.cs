using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Slots;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public class MockOutfitsService : IOutfitsService
    {
        private readonly Dictionary<int, OutfitData> mockDatabase = new ();

        public async UniTask<Dictionary<int, OutfitData>> GetOutfitsAsync(CancellationToken ct)
        {
            // Simulate network latency
            await UniTask.Delay(500, cancellationToken: ct);

            // Randomly decide if we should return some data
            if (mockDatabase.Count == 0 && Random.Range(0, 2) == 0)
            {
                // Create some fake data
                mockDatabase[0] = new OutfitData
                {
                    BodyShapeUrn = "urn:...", ThumbnailUrl = "some_url"
                };
                mockDatabase[2] = new OutfitData
                {
                    BodyShapeUrn = "urn:...", ThumbnailUrl = "some_url"
                };
            }

            return new Dictionary<int, OutfitData>(mockDatabase);
        }

        public async UniTask<OutfitData> SaveOutfitAsync(int slotIndex, OutfitData outfit, CancellationToken ct)
        {
            await UniTask.Delay(750, cancellationToken: ct);
            mockDatabase[slotIndex] = outfit;
            return outfit;
        }

        public async UniTask DeleteOutfitAsync(int slotIndex, CancellationToken ct)
        {
            await UniTask.Delay(300, cancellationToken: ct);
            mockDatabase.Remove(slotIndex);
        }
    }
}