using CRDT;
using DCL.ECSComponents;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.PlayerOriginated
{
    /// <summary>
    ///     Propagated to every scene root entity every frame
    /// </summary>
    public interface IPlayerInputEvents
    {
        List<InputEventEntry> Entries { get; }
        Ray Ray { get;}
        UnityEngine.RaycastHit RaycastHit { get;}
        CRDTEntity CrdtEntity { get;}

        public void Initialize(Ray ray,UnityEngine.RaycastHit raycastHit, CRDTEntity crdtEntity);
    }

    public class PlayerInputEvents : IPlayerInputEvents
    {
        public Ray Ray { get; private set; }
        public UnityEngine.RaycastHit RaycastHit { get; set; }
        public CRDTEntity CrdtEntity { get; set; }
        public List<InputEventEntry> Entries { get; private set; } = new List<InputEventEntry>();

        public void Initialize(Ray ray, UnityEngine.RaycastHit raycastHit, CRDTEntity crdtEntity)
        {
            Entries.Clear();
            CrdtEntity = crdtEntity;
            RaycastHit = raycastHit;
            Ray = ray;
        }

    }

    public readonly struct InputEventEntry
    {
        public readonly InputAction InputAction;
        public readonly PointerEventType PointerEventType;
        public readonly bool IsAtDistance;

        public InputEventEntry(InputAction inputAction, PointerEventType pointerEventType, bool isAtDistance)
        {
            InputAction = inputAction;
            PointerEventType = pointerEventType;
            IsAtDistance = isAtDistance;
        }
    }

}
