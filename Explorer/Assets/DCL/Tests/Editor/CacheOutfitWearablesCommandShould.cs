using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace DCL.Tests.Editor
{
    [TestFixture]
    public class CacheOutfitWearablesCommandShould
    {
        private const string RESOLVED_URN = "urn:decentraland:off-chain:base-avatars:aviatorstyle";
        private const string UNRESOLVED_URN = "urn:decentraland:off-chain:base-avatars:baggy_pullover";

        private IWearablesProvider wearablesProvider = null!;
        private IWearableStorage wearableStorage = null!;
        private CacheOutfitWearablesCommand command = null!;

        private IWearable resolvedWearable = null!;
        private IWearable unresolvedWearable = null!;
        private List<string> requestedPointers = null!;

        [SetUp]
        public void SetUp()
        {
            wearablesProvider = Substitute.For<IWearablesProvider>();
            wearableStorage = Substitute.For<IWearableStorage>();
            command = new CacheOutfitWearablesCommand(wearablesProvider, wearableStorage);

            resolvedWearable = new Wearable(new StreamableLoadingResult<WearableDTO>(new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = RESOLVED_URN,
                    data = new WearableDTO.WearableMetadataDto.DataDto { category = "hat" },
                },
            }));

            // A storage-cached placeholder whose DTO load failed or was cancelled: IsLoading == false, DTO == null.
            unresolvedWearable = IWearable.NewEmpty();
            unresolvedWearable.UpdateLoadingStatus(false);

            wearableStorage.TryGetElement(Arg.Any<URN>(), out Arg.Any<IWearable>())
                           .Returns(callInfo =>
                            {
                                string urn = callInfo.Arg<URN>().ToString();

                                if (urn == RESOLVED_URN)
                                {
                                    callInfo[1] = resolvedWearable;
                                    return true;
                                }

                                if (urn == UNRESOLVED_URN)
                                {
                                    callInfo[1] = unresolvedWearable;
                                    return true;
                                }

                                callInfo[1] = null;
                                return false;
                            });

            requestedPointers = new List<string>();

            wearablesProvider.GetByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>())
                             .Returns(callInfo =>
                              {
                                  foreach (URN pointer in callInfo.Arg<IReadOnlyCollection<URN>>())
                                      requestedPointers.Add(pointer.ToString());

                                  // A pointer that fails again comes back as an unresolved placeholder.
                                  List<IWearable> results = callInfo.Arg<List<IWearable>>();
                                  results.Add(unresolvedWearable);
                                  return UniTask.FromResult<IReadOnlyCollection<IWearable>?>(results);
                              });
        }

        [Test]
        public async Task KeepOutfitResultResolvedOnlyAndRefetchUnresolvedStorageHits()
        {
            LogAssert.ignoreFailingMessages = true;

            var result = new List<IWearable>();
            var urns = new List<URN> { RESOLVED_URN, UNRESOLVED_URN };

            await command.ExecuteAsync(urns, BodyShape.MALE, CancellationToken.None, result, useFullUrns: true);

            Assert.Contains(resolvedWearable, result, "The resolved storage hit must stay in the outfit result");

            for (var i = 0; i < result.Count; i++)
                Assert.IsNotNull(result[i].DTO, $"Outfit result[{i}] holds an unresolved wearable (DTO == null)");

            Assert.Contains(UNRESOLVED_URN, requestedPointers, "An unresolved storage hit must be re-fetched through the provider");
        }
    }
}
