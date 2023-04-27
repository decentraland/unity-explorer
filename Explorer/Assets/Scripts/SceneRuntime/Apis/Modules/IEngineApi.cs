using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;

public interface IEngineApi
{
    public UniTask<byte[]> CrdtSendToRenderer(byte[] data);

    public UniTask<byte[]> CrdtGetState();
}
