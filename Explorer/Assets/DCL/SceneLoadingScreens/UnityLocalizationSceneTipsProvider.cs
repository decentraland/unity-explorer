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
        private readonly StringTable fallbackTipsTable;
        private readonly TimeSpan defaultDuration;

        public UnityLocalizationSceneTipsProvider(
            LocalizedStringDatabase tipsDatabase,
            LocalizedAssetDatabase imagesDatabase,
            StringTable fallbackTipsTable,
            TimeSpan defaultDuration)
        {
            this.tipsDatabase = tipsDatabase;
            this.imagesDatabase = imagesDatabase;
            this.fallbackTipsTable = fallbackTipsTable;
            this.defaultDuration = defaultDuration;
        }

        public async UniTask<SceneTips> Get(Vector2Int parcelCoord, CancellationToken ct)
        {
            StringTable tipsTable = await tipsDatabase.GetTableAsync($"LoadingSceneTips-{parcelCoord.x},{parcelCoord.y}").Task
                                    ?? fallbackTipsTable;

            ct.ThrowIfCancellationRequested();

            AssetTable? imagesTable = await imagesDatabase.GetTableAsync($"LoadingSceneTipImages-{parcelCoord.x},{parcelCoord.y}").Task;
            ct.ThrowIfCancellationRequested();

            int tipCount = tipsTable.Count / 2;
            var tips = new SceneTips.Tip[tipCount];

            for (var i = 0; i < tipCount; i++)
            {
                Texture2D? texture = null;

                if (imagesTable != null)
                {
                    texture = await new LocalizedAsset<Texture2D>
                        {
                            TableReference = imagesTable.TableCollectionName,
                            TableEntryReference = $"IMAGE-{i}",
                        }.LoadAssetAsync()
                         .Task;
                }

                ct.ThrowIfCancellationRequested();

                string title = tipsTable.GetEntry($"TITLE-{i}").Value;
                string body = tipsTable.GetEntry($"BODY-{i}").Value;

                tips[i] = new SceneTips.Tip(title, body, texture);
            }

            return new SceneTips(defaultDuration, false, tips);
        }
    }
}
