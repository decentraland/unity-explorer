using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public interface IVfx
    {
        public void OnSpawn();

        public void OnReleased();

        public void SetPosition(Vector3 position);

        public UniTask WaitForCompletionAsync(CancellationToken ct);
    }
}