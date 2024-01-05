using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Web3Authentication.Signatures
{
    public interface IWeb3Signer : IDisposable
    {
        UniTask<Web3PersonalSignature> SignAsync(string payload, CancellationToken ct);
    }
}
