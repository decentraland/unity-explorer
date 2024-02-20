using DCL.Multiplayer.Movement.MessageBusMock;
using System;
using System.Collections;
using UnityEngine;

public class FadeOut : MonoBehaviour
{
    public MessageBus MessageBus;
    public CanvasGroup Group;

    // Start is called before the first frame update
    private void OnEnable()
    {
        StartCoroutine(FadeOutCoroutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator FadeOutCoroutine()
    {
        Group.alpha = 1;

        var t = 0f;

        while (t < MessageBus.PackageSentRate)
        {
            t += Time.deltaTime;
            Group.alpha -= t / MessageBus.PackageSentRate;
            yield return null;
        }

        Group.alpha = 0;
        gameObject.SetActive(false);
    }
}
