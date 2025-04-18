﻿using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Lambdas
{
    /// <summary>
    /// Caches paginated results until `Dispose` is called
    /// assuming the results are immutable while iterating
    /// </summary>
    public class LambdaResponsePagePointer<T, TAdditionalData> : IDisposable where T: PaginatedResponse
    {
        private readonly int pageSize;
        internal readonly Dictionary<int, (T page, DateTime retrievalTime)> cachedPages;
        private readonly ILambdaServiceConsumer<T, TAdditionalData> serviceConsumer;
        private readonly TimeSpan cacheExpiration;
        private readonly CancellationToken cancellationToken;
        private readonly string constEndPoint;

        internal bool isDisposed { get; private set; }

        internal IReadOnlyDictionary<int, (T page, DateTime retrievalTime)> CachedPages => cachedPages;

        /// <param name="constEndpoint"></param>
        /// <param name="pageSize"></param>
        /// <param name="cancellationToken">Pass Cancellation Token so the pointer will be automatically disposed on cancellation</param>
        /// <param name="consumer"></param>
        public LambdaResponsePagePointer(string constEndpoint, int pageSize, CancellationToken cancellationToken,
            ILambdaServiceConsumer<T, TAdditionalData> consumer) : this(constEndpoint, pageSize, cancellationToken, consumer, TimeSpan.FromDays(2))
        {
        }

        /// <param name="constEndpoint"></param>
        /// <param name="pageSize"></param>
        /// <param name="cancellationToken">Pass Cancellation Token so the pointer will be automatically disposed on cancellation</param>
        /// <param name="consumer"></param>
        /// <param name="cacheExpiration">Cache Lifetime</param>
        public LambdaResponsePagePointer(string constEndpoint, int pageSize, CancellationToken cancellationToken,
            ILambdaServiceConsumer<T, TAdditionalData> consumer, TimeSpan cacheExpiration)
        {
            this.pageSize = pageSize;
            this.cancellationToken = cancellationToken;
            this.constEndPoint = constEndpoint;

            cachedPages = DictionaryPool<int, (T page, DateTime retrievalTime)>.Get();
            serviceConsumer = consumer;
            this.cacheExpiration = cacheExpiration;

            cancellationToken.Register(Dispose, false);
        }

        /// <summary>
        /// Retrieves a page from the endpoint or cache
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public async UniTask<(T response, bool success)> GetPageAsync(int pageNum, TAdditionalData additionalData, CancellationToken localCancellationToken = default)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(LambdaResponsePagePointer<T, TAdditionalData>));

            if (cachedPages.TryGetValue(pageNum, out var cachedPage))
            {
                if (DateTime.Now < cachedPage.retrievalTime + cacheExpiration)
                    return (cachedPage.page, true);

                // cache expired
                cachedPages.Remove(pageNum);
            }

            var ct = this.cancellationToken;

            if (localCancellationToken != CancellationToken.None && !localCancellationToken.Equals(ct))
                ct = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, localCancellationToken).Token;

            var res = await serviceConsumer.CreateRequestAsync(constEndPoint, pageSize, pageNum, additionalData, ct);

            if (res.success)
            {
                cachedPages[pageNum] = (res.response, DateTime.Now);
            }

            return res;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            DictionaryPool<int, (T, DateTime)>.Release(cachedPages);
            isDisposed = true;
        }
    }
}
