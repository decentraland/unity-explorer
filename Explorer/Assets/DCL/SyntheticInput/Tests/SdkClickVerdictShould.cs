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

        private static readonly SdkDrainObservation CONSUMED = new (true, true, true);
        private static readonly SdkDrainObservation APPENDED_BUT_UNREAD = new (true, true, false);

        [Test]
        public void JudgeAnUndeclaredEventAsNotDeclaredWhateverWasObserved()
        {
            Assert.That(SdkClickVerdict.Judge(declared: false, in CONSUMED), Is.EqualTo(SdkEventVerdict.NotDeclared));
            Assert.That(SdkClickVerdict.Judge(declared: false, SdkDrainObservation.NotDrained), Is.EqualTo(SdkEventVerdict.NotDeclared));
        }

        [Test]
        public void JudgeAnAppendedAndReadEventAsConsumed()
        {
            Assert.That(SdkClickVerdict.Judge(declared: true, in CONSUMED), Is.EqualTo(SdkEventVerdict.Consumed));
        }

        [Test]
        public void JudgeNoDrainAsNotDrained()
        {
            Assert.That(SdkClickVerdict.Judge(declared: true, SdkDrainObservation.NotDrained), Is.EqualTo(SdkEventVerdict.NotDrained));
        }

        [Test]
        public void JudgeAResultTheSceneHasNotReadAsNotDrained()
        {
            Assert.That(SdkClickVerdict.Judge(declared: true, in APPENDED_BUT_UNREAD), Is.EqualTo(SdkEventVerdict.NotDrained),
                "a result the scene never read reached no handler");
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

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, in CONSUMED, in CONSUMED);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Info, Is.Null);
            Assert.That(result.ScreenRect, Is.EqualTo(RECT));
        }

        [Test]
        public void SucceedWithANoteWhenTheElementDeclaresOnlyThePress()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = false, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.NotDeclared };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, in CONSUMED, SdkDrainObservation.NotDrained);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Info, Does.Contain("PET_UP"));
        }

        [Test]
        public void FailWhenTheDeclaredPressWasNotDrainedAndSaySo()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = true, Down = SdkEventVerdict.NotDrained, Up = SdkEventVerdict.Consumed };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, SdkDrainObservation.NotDrained, in CONSUMED);

            Assert.That(result.Ok, Is.False, "a press the scene never received is not a delivered click");
            Assert.That(result.FailureReason, Does.Contain("did not consume the press"));
            Assert.That(result.FailureReason, Does.Contain("the release was still sent"));
        }

        [Test]
        public void FailWhenTheSceneHasNotReadTheDeclaredReleaseAndSaySo()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = true, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.NotDrained };

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, in CONSUMED, in APPENDED_BUT_UNREAD);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.FailureReason, Does.Contain("has not read the release"));
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

            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, in CONSUMED, in CONSUMED);

            Assert.That(result.Ok, Is.True, "a pointer already inside the element produces no enter, for a user too");
        }

        [Test]
        public void WriteTheDeliveryIntoTheJsonWithWireNames()
        {
            var delivery = new SdkClickDelivery { DeclaresDown = true, DeclaresUp = false, Enter = SdkEventVerdict.NotDrained, Down = SdkEventVerdict.Consumed, Up = SdkEventVerdict.NotDeclared };
            UiActionResult result = SdkClickVerdict.Build(in delivery, RECT, in CONSUMED, SdkDrainObservation.NotDrained);

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
