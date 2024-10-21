
using System;

namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class MovementsBlockedRestriction : SceneRestrictionBase, ICloneable
    {
        public bool DisableAll { get; set; }
        public bool DisableWalk { get; set; }
        public bool DisableJog { get; set; }
        public bool DisableRun { get; set; }
        public bool DisableJump { get; set; }
        public bool DisableEmote { get; set; }

        public MovementsBlockedRestriction() : base()
        {
            Type = SceneRestrictions.AVATAR_MOVEMENTS_BLOCKED;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            MovementsBlockedRestriction other = obj as MovementsBlockedRestriction;

            return this.DisableAll == other!.DisableAll
                   && this.DisableWalk == other!.DisableWalk
                   && this.DisableJog == other!.DisableJog
                   && this.DisableRun == other!.DisableRun
                   && this.DisableJump == other!.DisableJump
                   && this.DisableEmote == other!.DisableEmote;
        }

        public object Clone() =>
            new MovementsBlockedRestriction
            {
                Type = this.Type,
                Action = this.Action,
                DisableAll = this.DisableAll,
                DisableWalk = this.DisableWalk,
                DisableJog = this.DisableJog,
                DisableRun = this.DisableRun,
                DisableJump = this.DisableJump,
                DisableEmote = this.DisableEmote,
            };
    }
}
