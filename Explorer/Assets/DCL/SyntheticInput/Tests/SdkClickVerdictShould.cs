using DCL.ECSComponents;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SyntheticInput.Tests
{
    public class SdkClickVerdictShould
    {
        private static readonly Rect RECT = new (10f, 20f, 30f, 40f);

        private static SdkDrainObservation Consumed(PointerEventType type) =>
            new (true, type, true, true);

        private static SdkDrainObservation DrainedAs(PointerEventType type) =>
            new (true, type, false, true);

        [Test]
        public void JudgeAnUndeclaredEventAsNotDeclaredWhateverWasObserved()
        {
            Assert.That(SdkClickVerdict.Judge(PointerEventType.PetUp, declared: false, Consumed(PointerEventType.PetUp)), Is.EqualTo(SdkEventVerdict.NotDeclared));
            Assert.That(SdkClickVerdict.Judge(PointerEventType.PetUp, declared: false, SdkDrainObservation.NotDrained), Is.EqualTo(SdkEventVerdict.NotDeclared));
        }

        [Test]
        public void JudgeADrainedAndAppendedEventAsConsumed()
        {
            Assert.That(SdkClickVerdict.Judge(PointerEventType.PetDown, declared: true, Consumed(PointerEventType.PetDown)), Is.EqualTo(SdkEventVerdict.Consumed));
        }

        [Test]
        public void JudgeADrainOfAnotherEventAsDropped()
        {
            Assert.That(SdkClickVerdict.Judge(PointerEventType.PetDown, declared: true, DrainedAs(PointerEventType.PetHoverLeave)), Is.EqualTo(SdkEventVerdict.Dropped));
        }

        [Test]
        public void JudgeADrainWithoutAResultAsDropped()
        {
            Assert.That(SdkClickVerdict.Judge(PointerEventType.PetDown, declared: true, DrainedAs(PointerEventType.PetDown)), Is.EqualTo(SdkEventVerdict.Dropped));
        }

        [Test]
        public void JudgeNoDrainAsNotDrained()
        {
            Assert.That(SdkClickVerdict.Judge(PointerEventType.PetDown, declared: true, SdkDrainObservation.NotDrained), Is.EqualTo(SdkEventVerdict.NotDrained));
        }

        [Test]
        public void DeclareOnlyTheTypesTheEntityLists()
        {
            var events = new PBPointerEvents();
            events.PointerEvents.Add(new PBPointerEvents.Types.Entry { EventType = PointerEventType.PetDown, EventInfo = new PBPointerEvents.Types.Info() });

            Assert.That(SdkClickVerdict.Declares(events, PointerEventType.PetDown), Is.True);
            Assert.That(SdkClickVerdict.Declares(events, PointerEventType.PetUp), Is.False);
            Assert.That(SdkClickVerdict.Declares(null, PointerEventType.PetDown), Is.False);
        }

        [Test]
        public void SucceedWhenEveryDeclaredEventWasConsumed()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = true, Enter = SdkEventVerdict.NotDeclared, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.Consumed };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, Consumed(PointerEventType.PetDown), Consumed(PointerEventType.PetUp));

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Info, Is.Null);
            Assert.That(result.ScreenRect, Is.EqualTo(RECT));
        }

        [Test]
        public void SucceedWithANoteWhenTheElementDeclaresOnlyThePress()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = false, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.NotDeclared };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, Consumed(PointerEventType.PetDown), SdkDrainObservation.NotDrained);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Info, Does.Contain("PET_UP"));
        }

        [Test]
        public void FailWhenTheDeclaredPressWasDroppedAndNameWhatWasDrainedInstead()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = true, Down = SdkEventVerdict.Dropped, Up = SdkEventVerdict.Consumed };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, DrainedAs(PointerEventType.PetHoverLeave), Consumed(PointerEventType.PetUp));

            Assert.That(result.Ok, Is.False, "a press the scene never received is not a delivered click");
            Assert.That(result.FailureReason, Does.Contain("PetHoverLeave"));
            Assert.That(result.FailureReason, Does.Contain("press"));
        }

        [Test]
        public void FailWhenTheElementDeclaresNeitherPressNorRelease()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = false, DeclaresUp = false, Down = SdkEventVerdict.NotDeclared, Up = SdkEventVerdict.NotDeclared };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, SdkDrainObservation.NotDrained, SdkDrainObservation.NotDrained);

            Assert.That(result.Ok, Is.False, "a bare ok on an element with no handler would be the lie the verdict exists to prevent");
            Assert.That(result.FailureReason, Does.Contain("no PET_DOWN or PET_UP handler"));
        }

        [Test]
        public void NotLetTheEnterDecideTheVerdict()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = true, Enter = SdkEventVerdict.NotDrained, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.Consumed };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, Consumed(PointerEventType.PetDown), Consumed(PointerEventType.PetUp));

            Assert.That(result.Ok, Is.True, "a pointer already inside the element produces no enter, for a user too");
        }

        [Test]
        public void WriteTheDeliveryIntoTheJsonWithWireNames()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = false, Enter = SdkEventVerdict.NotDrained, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.NotDeclared };
            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, Consumed(PointerEventType.PetDown), SdkDrainObservation.NotDrained);

            JObject json = result.ToJson("Free");

            Assert.That(json["delivered"]?["enter"]?.ToString(), Is.EqualTo("not_drained"));
            Assert.That(json["delivered"]?["down"]?.ToString(), Is.EqualTo("consumed"));
            Assert.That(json["delivered"]?["up"]?.ToString(), Is.EqualTo("not_declared"));
        }

        [Test]
        public void WriteNoDeliveryForAGestureThatHasNone()
        {
            JObject json = UiActionResult.Success(RECT).ToJson("Free");

            Assert.That(json["delivered"], Is.Null);
        }
    }
}
