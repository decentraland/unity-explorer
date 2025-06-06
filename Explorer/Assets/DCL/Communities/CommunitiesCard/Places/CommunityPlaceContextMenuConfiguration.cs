using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Places
{
    [CreateAssetMenu(fileName = "CommunityPlaceContextMenuSettings", menuName = "DCL/Communities/Places/ContextMenuSettings")]
    [Serializable]
    public class CommunityPlaceContextMenuConfiguration : ScriptableObject
    {
        [SerializeField] private int contextMenuWidth = 250;
        [Space(10)]
        [SerializeField] private Sprite shareSprite;
        [SerializeField] private string shareText = "Share on X";
        [Space(10)]
        [SerializeField] private Sprite copyLinkSprite;
        [SerializeField] private string copyLinkText = "Copy link";

        public int ContextMenuWidth => contextMenuWidth;

        public Sprite ShareSprite => shareSprite;
        public string ShareText => shareText;

        public Sprite CopyLinkSprite => copyLinkSprite;
        public string CopyLinkText => copyLinkText;
    }
}
