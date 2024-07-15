using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.GLTF
{
    public struct GetGLTFIntention: ILoadingIntention, IEquatable<GetGLTFIntention>
    {
        public string? Hash;
        public readonly string? Name; // File path

        public CancellationTokenSource CancellationTokenSource { get; }
        public CommonLoadingArguments CommonArguments { get; set; }

        private GetGLTFIntention(
            string? name = null,
            string? hash = null,
            CancellationTokenSource cancellationTokenSource = null)
        {
            Name = name;
            Hash = hash;
            CancellationTokenSource = cancellationTokenSource;
            CommonArguments = new CommonLoadingArguments(
                URLAddress.EMPTY,
                cancellationTokenSource: cancellationTokenSource);
        }

        public static GetGLTFIntention Create(string name, string hash) => new (name: name, hash: hash);

        public bool Equals(GetGLTFIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;
    }
}
