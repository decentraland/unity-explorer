using System.Collections;
using UnityEngine;

namespace Utility
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator coroutine);
    }
}
