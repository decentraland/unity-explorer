using System.Collections;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.UI
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator coroutine);
    }
}
