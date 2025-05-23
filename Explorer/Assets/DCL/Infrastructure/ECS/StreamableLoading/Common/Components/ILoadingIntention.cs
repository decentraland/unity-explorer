﻿using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace ECS.StreamableLoading.Common.Components
{
    public interface IAssetIntention
    {
        CancellationTokenSource CancellationTokenSource { get; }
    }

    public interface ILoadingIntention : IAssetIntention
    {
        bool DisableDiskCache => false;
        CommonLoadingArguments CommonArguments { get; set; }
    }

    public interface IPointersLoadingIntention : ILoadingIntention
    {
        IReadOnlyList<URN> Pointers { get; }
    }

    public static class LoadingIntentionExtensions
    {
        public static bool IsCancelled<T>(this ref T intention) where T: struct, ILoadingIntention =>
            intention.CancellationTokenSource.IsCancellationRequested;

        public static void SetURL<T>(this ref T loadingIntention, URLAddress url) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.URL = url;
            loadingIntention.CommonArguments = ca;
        }

        public static void RemoveCurrentSource<T>(this ref T loadingIntention) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.PermittedSources.RemoveFlag(ca.CurrentSource);
            loadingIntention.CommonArguments = ca;
        }

        public static void RemovePermittedSource<T>(this ref T loadingIntention, AssetSource source) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.PermittedSources.RemoveFlag(source);
            loadingIntention.CommonArguments = ca;
        }

        public static void SetAttempts<T>(this ref T loadingIntention, int attempts) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.Attempts = attempts;
            loadingIntention.CommonArguments = ca;
        }

        public static void SetSources<T>(this ref T loadingIntention, AssetSource permittedSources, AssetSource currentSource) where T: struct, ILoadingIntention
        {
            CommonLoadingArguments ca = loadingIntention.CommonArguments;
            ca.PermittedSources = permittedSources;
            ca.CurrentSource = currentSource;
            loadingIntention.CommonArguments = ca;
        }

        /// <summary>
        ///     Only assets downloaded from web can be cached on disk, otherwise the asset is already stored locally on disk
        /// </summary>
        public static bool IsQualifiedForDiskCache<T>(this ref T loadingIntention) where T: struct, ILoadingIntention =>
            loadingIntention.CommonArguments.CurrentSource == AssetSource.WEB && !loadingIntention.DisableDiskCache;

        public static bool AreUrlEquals<TIntention>(this TIntention intention, TIntention other) where TIntention: struct, ILoadingIntention =>
            intention.CommonArguments.URL == other.CommonArguments.URL;

        public static bool TryCancelByRequest<TIntention, TStreamableResult>(
            this TIntention intention,
            World world,
            ReportData reportData,
            Entity entity,
            Func<TIntention, string> errorMessage
        ) where TIntention: IAssetIntention
        {
            if (intention.CancellationTokenSource.IsCancellationRequested)
            {
                if (world.Has<StreamableLoadingResult<TStreamableResult>>(entity) == false)
                    world.Add(
                        entity,
                        new StreamableLoadingResult<TStreamableResult>(
                            reportData, new OperationCanceledException(errorMessage(intention)!)
                        )
                    );

                return true;
            }

            return false;
        }
    }
}
