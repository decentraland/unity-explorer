using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace DCL.SceneLoadingScreens
{
    public class UnityLocalizationSceneTipsProvider : ISceneTipsProvider
    {
        private readonly LocalizedStringDatabase tipsDatabase;
        private readonly LocalizedAssetDatabase imagesDatabase;
        private readonly string fallbackTipsTable;
        private readonly string fallbackImagesTable;
        private readonly TimeSpan defaultDuration;

        private SceneTips fallbackTips;

        public UnityLocalizationSceneTipsProvider(
            LocalizedStringDatabase tipsDatabase,
            LocalizedAssetDatabase imagesDatabase,
            string fallbackTipsTable,
            string fallbackImagesTable,
            TimeSpan defaultDuration)
        {
            this.tipsDatabase = tipsDatabase;
            this.imagesDatabase = imagesDatabase;
            this.fallbackTipsTable = fallbackTipsTable;
            this.fallbackImagesTable = fallbackImagesTable;
            this.defaultDuration = defaultDuration;
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            StringTable tipsTable = await tipsDatabase.GetTableAsync(fallbackTipsTable).Task;

            ct.ThrowIfCancellationRequested();

            AssetTable imagesTable = await imagesDatabase.GetTableAsync(fallbackImagesTable).Task;

            ct.ThrowIfCancellationRequested();

            fallbackTips = await GetAsync(tipsTable, imagesTable, ct);
        }

        public async UniTask<SceneTips> GetAsync(CancellationToken ct) =>

            // TODO: we will need specific scene tips in the future, but its disabled at the moment
            /*StringTable tipsTable = await tipsDatabase.GetTableAsync($"LoadingSceneTips-{parcelCoord.x},{parcelCoord.y}").Task
                                    ?? await tipsDatabase.GetTableAsync(fallbackTipsTable).Task;

            ct.ThrowIfCancellationRequested();

            AssetTable? imagesTable = await imagesDatabase.GetTableAsync($"LoadingSceneTipImages-{parcelCoord.x},{parcelCoord.y}").Task
                                      ?? await imagesDatabase.GetTableAsync(fallbackImagesTable).Task;

            ct.ThrowIfCancellationRequested();

            return await Get(tipsTable, imagesTable, ct);*/
            fallbackTips;

        private async UniTask<SceneTips> GetAsync(StringTable tipsTable, AssetTable? imagesTable, CancellationToken ct)
        {
            int tipCount = tipsTable.Count / 2;
            var tips = new SceneTips.Tip[tipCount];

            for (var i = 0; i < tipCount; i++)
            {
                Sprite? sprite = null;

                if (imagesTable != null)
                {
                    sprite = await new LocalizedAsset<Sprite>
                        {
                            TableReference = imagesTable.TableCollectionName,
                            TableEntryReference = $"IMAGE-{i}",
                        }.LoadAssetAsync()
                         .Task;
                }

                ct.ThrowIfCancellationRequested();

                string title = tipsTable.GetEntry($"TITLE-{i}").Value;
                string body = tipsTable.GetEntry($"BODY-{i}").Value;

                tips[i] = new SceneTips.Tip(title, body, sprite);
            }

            return new SceneTips(defaultDuration, random: false, tips);
        }
    }
}
