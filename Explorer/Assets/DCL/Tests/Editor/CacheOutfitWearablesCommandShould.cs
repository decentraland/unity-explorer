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
        private IWearable bodyShapeWearable = null!;
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

            // The equipped body shape is always a resolved storage hit by the time an outfit equips.
            bodyShapeWearable = new Wearable(new StreamableLoadingResult<WearableDTO>(new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = BodyShape.MALE,
                    data = new WearableDTO.WearableMetadataDto.DataDto { category = "body_shape" },
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

                                if (urn == BodyShape.MALE)
                                {
                                    callInfo[1] = bodyShapeWearable;
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

                                  // The IWearablesProvider contract doesn't promise resolved-only results;
                                  // simulate an unresolved placeholder coming back.
                                  List<IWearable> results = callInfo.ArgAt<List<IWearable>?>(3) ?? new List<IWearable>();
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

            Assert.Contains(UNRESOLVED_URN, requestedPointers, "An unresolved storage hit must be re-requested through the provider");
        }

        [Test]
        public async Task CompleteOnDtoSettleAndApplyRefetchedStorageHitWithoutAwaitingAssets()
        {
            LogAssert.ignoreFailingMessages = true;

            var assetFetchNeverCompletes = new UniTaskCompletionSource<IReadOnlyCollection<IWearable>?>();

            wearablesProvider.GetByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IWearable>?>())
                             .Returns(_ =>
                              {
                                  // The DTO batch lands (resolving the placeholder in storage) while the
                                  // returned task keeps downloading assets and never completes.
                                  unresolvedWearable.ApplyAndMarkAsLoaded(new WearableDTO
                                  {
                                      metadata = new WearableDTO.WearableMetadataDto
                                      {
                                          id = UNRESOLVED_URN,
                                          data = new WearableDTO.WearableMetadataDto.DataDto { category = "upper_body" },
                                      },
                                  });

                                  return assetFetchNeverCompletes.Task;
                              });

            var result = new List<IWearable>();
            var urns = new List<URN> { RESOLVED_URN, UNRESOLVED_URN };

            UniTask execute = command.ExecuteAsync(urns, BodyShape.MALE, CancellationToken.None, result, useFullUrns: true);
            int winner = await UniTask.WhenAny(execute, PumpAsync());

            Assert.AreEqual(0, winner, "Outfit DTO caching must complete once every missing DTO settles, without awaiting the provider's asset fetch");
            Assert.Contains(resolvedWearable, result, "The resolved storage hit must stay in the outfit result");
            Assert.Contains(unresolvedWearable, result, "A storage hit whose DTO resolves during the re-fetch must be applied to the outfit");

            for (var i = 0; i < result.Count; i++)
                Assert.IsNotNull(result[i].DTO, $"Outfit result[{i}] holds an unresolved wearable (DTO == null)");

            assetFetchNeverCompletes.TrySetCanceled();
        }

        private static async UniTask PumpAsync()
        {
            for (var i = 0; i < 120; i++)
                await UniTask.Yield();
        }
    }
}
