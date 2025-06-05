#nullable enable

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.WebRequests.WebContentSizes
{
    public class SeveralWebContentSizes : IWebContentSizes
    {
        private readonly IReadOnlyList<IWebContentSizes> list;

        public SeveralWebContentSizes(params IWebContentSizes[] list) : this(list as IReadOnlyList<IWebContentSizes>) { }

        public SeveralWebContentSizes(IReadOnlyList<IWebContentSizes> list)
        {
            this.list = list;
        }

        public async UniTask<bool> IsOkSizeAsync(Uri url, CancellationToken cancellationToken)
        {
            foreach (var webContentSizes in list)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                if (await webContentSizes.IsOkSizeAsync(url, cancellationToken))
                    return true;
            }

            return false;
        }
    }
}
