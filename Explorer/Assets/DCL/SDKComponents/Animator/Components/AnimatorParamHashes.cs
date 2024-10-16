using System.Collections.Generic;

namespace DCL.SDKComponents.Animator.Components
{
    public struct AnimatorParamHashes
    {
        public readonly Dictionary<string, StateParamHashes> Hashes;

        public AnimatorParamHashes(Dictionary<string, StateParamHashes> hashes)
        {
            Hashes = hashes;
        }

        public struct StateParamHashes
        {
            public int LayerIndex;
            public int TriggerParamHash;
            public int EnabledParamHash;
            public int LoopParamHash;
        }
    }
}
