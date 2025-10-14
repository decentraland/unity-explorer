using Cysharp.Threading.Tasks;
using Mscc.GenerativeAI;
using System;
using UnityEngine;

public class MCPHost : MonoBehaviour
{
    public string modelName;

    [ContextMenu("TEST AI")]
    public void TestGoogleAPI() =>
        CallG().Forget();

    private static async UniTask CallG()
    {
        string apiKey = "AIzaSyANdi9MKBS1xb73K-A-1orCwGTeNLtbrRQ";
        var googleAI = new GoogleAI(apiKey);

        GenerativeModel model = googleAI.GenerativeModel();
        string prompt = "Объясни, что такое API, простыми словами для новичка.";
        Debug.Log($"Ваш запрос: {prompt}\n");

        try
        {
            GenerateContentResponse response = await model.GenerateContent(prompt);
            Debug.Log($"Ответ Gemini: {response.Text}");
        }
        catch (Exception ex) { Debug.Log($"Произошла ошибка: {ex.Message}"); }
    }
}
