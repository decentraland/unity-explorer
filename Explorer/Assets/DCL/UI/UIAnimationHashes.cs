using UnityEngine;

namespace DCL.UI
{
    public static class UIAnimationHashes
    {
        public static readonly int HOVER = Animator.StringToHash("Hover");
        public static readonly int UNHOVER = Animator.StringToHash("Unhover");
        public static readonly int OUT = Animator.StringToHash("Out");
        public static readonly int IN = Animator.StringToHash("In");
        public static readonly int JUMP_IN = Animator.StringToHash("Jump");
        public static readonly int PRESSED = Animator.StringToHash("Pressed");
        public static readonly int LOADING = Animator.StringToHash("Loading");
        public static readonly int EXPAND = Animator.StringToHash("Expand");
        public static readonly int COLLAPSE = Animator.StringToHash("Collapse");
        public static readonly int LOADED = Animator.StringToHash("Loaded");
        public static readonly int TO_LEFT = Animator.StringToHash("ToLeft");
        public static readonly int TO_RIGHT = Animator.StringToHash("ToRight");
        public static readonly int ACTIVE = Animator.StringToHash("Active");
        public static readonly int TO_OTHER = Animator.StringToHash("Different");
    }
}
