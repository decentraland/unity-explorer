using System;
using UnityEngine;
using UnityEngine.UI;

public class TestScript : MonoBehaviour
{
    public Text NetworkTimeText;

    public void GetNetworkTime()
    {
        using (NtpAssetStore assetStore = new NtpAssetStore("time.windows.com"))
        {
            DateTime dt = assetStore.GetNetworkTime();

            string timeUTC = "Network Time(UTC): " + dt.ToString();

            string time = "Network Time: " + dt.ToLocalTime().ToString();

            string server = "Server: time.windows.com";

            NetworkTimeText.text = timeUTC + Environment.NewLine + time + Environment.NewLine + server;
        }
    }
}
