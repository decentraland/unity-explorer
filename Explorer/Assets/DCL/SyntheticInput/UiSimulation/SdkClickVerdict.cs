using DCL.ECSComponents;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>What the scene did with one pointer event sent to a scene-UI element.</summary>
    public enum SdkEventVerdict : byte
    {
        /// <summary>The entity declares no handler for the event; the scene was told nothing, as for a user.</summary>
        NotDeclared,

        /// <summary>The scene's write pass appended a result for the event and the scene read it.</summary>
        Consumed,

        /// <summary>
        ///     No result was appended for the event in time (the element's queue was never written, or the event never
        ///     reached the element), or the scene has not read the result yet.
        /// </summary>
        NotDrained,
    }

    /// <summary>One observation of the element's queue and drain log after an event was sent or queued.</summary>
    public readonly struct SdkDrainObservation
    {
        /// <summary>The write pass wrote the element's queue after the event was sent (always true once <see cref="Appended" /> is).</summary>
        public readonly bool Drained;

        /// <summary>A result of the event's type was appended since the event was sent.</summary>
        public readonly bool Appended;

        /// <summary>The scene's tick moved past the result, so a handler could have run.</summary>
        public readonly bool SceneRead;

        public SdkDrainObservation(bool drained, bool appended, bool sceneRead)
        {
            Drained = drained;
            Appended = appended;
            SceneRead = sceneRead;
        }

        public static SdkDrainObservation NotDrained => new (false, false, false);
    }

    /// <summary>The verdict of each event of a click, with what the element declared.</summary>
    public struct SdkClickDelivery
    {
        public SdkEventVerdict Enter;
        public SdkEventVerdict Down;
        public SdkEventVerdict Up;

        public bool DeclaresDown;
        public bool DeclaresUp;

        public JObject ToJson() =>
            new ()
            {
                ["enter"] = SdkClickVerdict.ToWire(Enter),
                ["down"] = SdkClickVerdict.ToWire(Down),
                ["up"] = SdkClickVerdict.ToWire(Up),
            };
    }

    /// <summary>
    ///     Judges a scene-UI click from the element's drain log, so the result claims only what the scene received.
    /// </summary>
    public static class SdkClickVerdict
    {
        public static bool Declares(PBPointerEvents? events, PointerEventType type)
        {
            if (events == null)
                return false;

            foreach (PBPointerEvents.Types.Entry? entry in events.PointerEvents)
            {
                if (entry != null && entry.EventType == type)
                    return true;
            }

            return false;
        }

        public static SdkEventVerdict Judge(bool declared, in SdkDrainObservation observed)
        {
            if (!declared)
                return SdkEventVerdict.NotDeclared;

            // A result the scene has not read yet reached no handler either.
            return observed.Appended && observed.SceneRead ? SdkEventVerdict.Consumed : SdkEventVerdict.NotDrained;
        }

        /// <summary>
        ///     Ok only when every event the element declares among down and up was consumed. The enter never decides it:
        ///     a pointer already inside the element produces none, for a user too.
        /// </summary>
        public static UiActionResult Build(in SdkClickDelivery delivery, Rect imageRect, in SdkDrainObservation downObserved, in SdkDrainObservation upObserved)
        {
            UiActionResult result;

            if (!delivery.DeclaresDown && !delivery.DeclaresUp)
            {
                result = UiActionResult.Failure(
                    "the element declares no PET_DOWN or PET_UP handler (no onMouseDown/onMouseUp), so the click told the scene nothing",
                    null, imageRect);
            }
            else if (delivery.DeclaresDown && delivery.Down != SdkEventVerdict.Consumed)
                result = UiActionResult.Failure(Describe("press", delivery.Down, in downObserved) + "; the release was still sent", null, imageRect);
            else if (delivery.DeclaresUp && delivery.Up != SdkEventVerdict.Consumed)
                result = UiActionResult.Failure(Describe("release", delivery.Up, in upObserved), null, imageRect);
            else
            {
                result = UiActionResult.Success(imageRect);

                if (!delivery.DeclaresUp)
                    result.Info = "the element declares no PET_UP handler, so only the press reached the scene";
                else if (!delivery.DeclaresDown)
                    result.Info = "the element declares no PET_DOWN handler, so only the release reached the scene";
            }

            result.SdkDelivery = delivery;
            return result;
        }

        public static string ToWire(SdkEventVerdict verdict) =>
            verdict switch
            {
                SdkEventVerdict.Consumed => "consumed",
                SdkEventVerdict.NotDrained => "not_drained",
                _ => "not_declared",
            };

        private static string Describe(string eventName, SdkEventVerdict verdict, in SdkDrainObservation observed) =>
            verdict switch
            {
                SdkEventVerdict.NotDrained when observed.Appended => $"the scene has not read the {eventName} yet (is the scene paused?)",
                SdkEventVerdict.NotDrained => $"the scene did not consume the {eventName} (is the scene paused, or did the event never reach the element?)",
                _ => $"the {eventName} was not delivered",
            };
    }
}
