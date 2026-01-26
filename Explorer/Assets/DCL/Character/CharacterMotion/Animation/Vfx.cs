using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public abstract class Vfx<T> : IVfx where T : Component
    {
        protected T target;

        protected Vfx(T target)
        {
            this.target = target;
        }

        public virtual void OnSpawn() =>
            target.gameObject.SetActive(true);

        public virtual void OnReleased() =>
            target.gameObject.SetActive(false);

        public void SetPosition(Vector3 position) =>
            target.transform.position = position;

        public abstract UniTask WaitForCompletion(CancellationToken ct);
    }
}