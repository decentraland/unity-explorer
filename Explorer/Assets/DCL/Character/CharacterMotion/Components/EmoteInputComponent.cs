using DCL.Input.Component;

namespace DCL.CharacterMotion.Components
{
    public struct EmoteInputComponent : IInputComponent
    {
        public int TriggeredEmoteSlot;

        public EmoteInputComponent(int triggeredEmoteSlot)
        {
            this.TriggeredEmoteSlot = triggeredEmoteSlot;
        }
    }
}
