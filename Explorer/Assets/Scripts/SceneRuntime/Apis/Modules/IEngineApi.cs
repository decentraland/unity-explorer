using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;

public interface IEngineApi
{
    public UniTask<ITypedArray<byte>> CrdtSendToRenderer(ITypedArray<byte> data);

    public UniTask<ITypedArray<byte>> CrdtGetState();
}
