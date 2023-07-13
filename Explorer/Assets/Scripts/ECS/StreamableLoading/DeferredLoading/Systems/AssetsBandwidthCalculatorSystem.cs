using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.DeferredLoading.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareAssetBundleLoadingParametersSystem))]
    [UpdateBefore(typeof(AssetsDeferredLoadingSystem))]
    public partial class AssetsBandwidthCalculatorSystem : BaseUnityLoopSystem
    {
        public abstract class AssetBandwidthCalculator
        {
            public abstract void Update();
        }
        public class AssetBandwidthCalculator<TAsset, TIntention> : AssetBandwidthCalculator where TIntention: struct, ILoadingIntention
        {
            private static readonly QueryDescription QUERY_BANDWIDTH = new QueryDescription()
                                                                     .WithAll<TIntention>()
                                                                     .WithNone<HeadRequestDone, HeadRequestInProgress, StreamableLoadingResult<TAsset>>();

            private static readonly QueryDescription QUERY_BANDWIDTH_RESULT = new QueryDescription()
                                                                            .WithAll<TIntention, HeadRequestInProgress, UnityWebRequest>()
                                                                            .WithNone<StreamableLoadingResult<TAsset>>();

            private World world;

            public AssetBandwidthCalculator(World world)
            {
                this.world = world;
            }

            public override void Update()
            {
                world.Query<TIntention>(QUERY_BANDWIDTH, GetBandwidthIntention);
                world.Query<TIntention, UnityWebRequest>(QUERY_BANDWIDTH_RESULT, GetBandwidthResultIntention);
            }

            private void GetBandwidthIntention(in Entity entity, ref TIntention intention)
            {
                UnityWebRequest webRequest = UnityWebRequest.Head(intention.CommonArguments.URL);
                webRequest.method = UnityWebRequest.kHttpVerbGET;
                webRequest.SendWebRequest();
                world.Add(entity, new HeadRequestInProgress(), webRequest);
            }

            private void GetBandwidthResultIntention(in Entity entity, ref TIntention intention, ref UnityWebRequest unityWebRequest)
            {
                if (unityWebRequest.isDone)
                {
                    int budgetCost = 1;
                    if (unityWebRequest.result == UnityWebRequest.Result.Success)
                    {
                        string contentLengthHeader = unityWebRequest.GetResponseHeader("Content-Length");
                        if (!string.IsNullOrEmpty(contentLengthHeader))
                        {
                            budgetCost = (int)Math.Ceiling(float.Parse(contentLengthHeader) / (1024 * 1024));
                        }
                        else
                            ReportHub.LogWarning(ReportCategory.STREAMABLE_LOADING,$"Content-Length header not found in the response for {intention.CommonArguments.URL}");
                    }
                    else
                        ReportHub.LogWarning(ReportCategory.STREAMABLE_LOADING,$"Head request failed for {intention.CommonArguments.URL}");

                    intention.SetBudgetCost(budgetCost);

                    world.Add<HeadRequestDone>(entity);
                    world.Remove<HeadRequestInProgress, UnityWebRequest>(entity);
                }
            }
        }

        private readonly AssetBandwidthCalculator[] componentHandlers;

        public AssetsBandwidthCalculatorSystem(World world) : base(world)
        {
            componentHandlers = new AssetBandwidthCalculator[]
            {
                new AssetBandwidthCalculator<AssetBundleData, GetAssetBundleIntention>(world),
                new AssetBandwidthCalculator<Texture2D, GetTextureIntention>(world)
            };
        }

        protected override void Update(float t)
        {
            foreach (AssetBandwidthCalculator assetBandwidthCalculator in componentHandlers)
                assetBandwidthCalculator.Update();
        }

    }
}
