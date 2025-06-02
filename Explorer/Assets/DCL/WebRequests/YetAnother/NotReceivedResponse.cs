using Best.HTTP.Response;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DCL.WebRequests
{
    public class NotReceivedResponse : IWebRequestResponse
    {
        public static readonly NotReceivedResponse INSTANCE = new ();

        public bool Received => false;

        public UniTask<string> GetTextAsync(CancellationToken ct) =>
            UniTask.FromResult(string.Empty);

        public string Error => string.Empty;

        public UniTask<byte[]> GetDataAsync(CancellationToken ct) =>
            UniTask.FromResult(Array.Empty<byte>());

        public int StatusCode => HTTPStatusCodes.Processing;

        public bool IsSuccess => false;

        public ulong DataLength => 0;

        public UniTask<Stream> GetCompleteStreamAsync(CancellationToken ct) =>
            UniTask.FromResult(Stream.Null);

        public string? GetHeader(string headerName) =>
            null;

        public Dictionary<string, string>? FlattenHeaders() =>
            null;
    }
}
