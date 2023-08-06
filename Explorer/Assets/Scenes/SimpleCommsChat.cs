using Comms.Systems;
using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    WSCommsRoom comms = new WSCommsRoom();

    public TMP_InputField ChatInput;
    public TextMeshProUGUI ChatBox;

    async UniTask Start()
    {
        comms.OnChatMessage += (_, message) =>
        {
            ChatBox.text += $"{message}\n";
        };

        await comms.Connect(new Uri("wss://ws-room-service.decentraland.org/rooms/goerli-plaza-main"));

        while (true)
        {
            if (!comms.IsConnected()) continue;

            await comms.ProcessNextMessage();
            await UniTask.NextFrame();
        }
    }

    public async void Update()
    {
        //Detect when the Return key is pressed down
        if (Input.GetKeyDown(KeyCode.Return))
        {
            await comms.SendChat(ChatInput.text.ToString());
            ChatBox.text += $"{ChatInput.text}\n";
            ChatInput.text = "";
            ChatInput.Select();
        }
    }
}
