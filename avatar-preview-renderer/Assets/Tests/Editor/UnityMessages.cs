using System.Reflection;
using UnityEngine;

namespace Tests
{
    /// <summary>
    /// Calls a behaviour's private Unity message directly: outside play mode Unity refuses to run one
    /// through <c>SendMessage</c>.
    /// </summary>
    internal static class UnityMessages
    {
        public static void Invoke(MonoBehaviour target, string method)
        {
            target.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(target, null);
        }
    }
}
