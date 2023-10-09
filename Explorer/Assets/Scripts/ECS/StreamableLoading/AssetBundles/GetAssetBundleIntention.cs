﻿using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using JetBrains.Annotations;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleIntention : ILoadingIntention, IEquatable<GetAssetBundleIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public string Hash;

        /// <summary>
        ///     Name not resolved into <see cref="Hash" />
        /// </summary>
        public readonly string Name;

        /// <summary>
        ///     Manifest can be null if <see cref="CommonArguments" />.<see cref="CommonLoadingArguments.PermittedSources" /> does not contain <see cref="AssetSource.WEB" />
        /// </summary>
        [CanBeNull]
        public SceneAssetBundleManifest Manifest;

        /// <summary>
        ///     Sanitized hash used by Unity's Caching system,
        /// </summary>
        internal Hash128? cacheHash;

        /// <param name="name">Name is resolved into Hash before loading by the manifest</param>
        /// <param name="hash">Hash of the asset, if it is provided manifest is not checked</param>
        /// <param name="permittedSources">Sources from which systems will try to load</param>
        /// <param name="assetBundleManifest"></param>
        /// <param name="customEmbeddedSubDirectory"></param>
        /// <param name="cancellationTokenSource"></param>
        private GetAssetBundleIntention(string name = null, string hash = null,
            AssetSource permittedSources = AssetSource.ALL, SceneAssetBundleManifest assetBundleManifest = null,
            URLSubdirectory customEmbeddedSubDirectory = default,
            CancellationTokenSource cancellationTokenSource = null)
        {
            Name = name;
            Hash = hash;

            // Don't resolve URL here

            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, customEmbeddedSubDirectory, permittedSources: permittedSources, cancellationTokenSource: cancellationTokenSource);
            cacheHash = null;
            Manifest = assetBundleManifest;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public static GetAssetBundleIntention FromName(string name, AssetSource permittedSources = AssetSource.ALL, URLSubdirectory customEmbeddedSubDirectory = default) =>
            new (name: name, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory);

        public static GetAssetBundleIntention FromHash(string hash, AssetSource permittedSources = AssetSource.ALL, SceneAssetBundleManifest manifest = null, URLSubdirectory customEmbeddedSubDirectory = default) =>
            new (hash: hash, permittedSources: permittedSources, assetBundleManifest: manifest, customEmbeddedSubDirectory: customEmbeddedSubDirectory);

        public static GetAssetBundleIntention FromHash(string hash, CancellationTokenSource cancellationTokenSource, AssetSource permittedSources = AssetSource.ALL,
            SceneAssetBundleManifest manifest = null, URLSubdirectory customEmbeddedSubDirectory = default) =>
            new (hash: hash, permittedSources: permittedSources, assetBundleManifest: manifest, customEmbeddedSubDirectory: customEmbeddedSubDirectory, cancellationTokenSource: cancellationTokenSource);

        public bool Equals(GetAssetBundleIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;

        public override bool Equals(object obj) =>
            obj is GetAssetBundleIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Hash), Name);

        public override string ToString() =>
            $"Get Asset Bundle: {Name} ({Hash})";
    }
}
