using System;
using DCL.ECSComponents;
using Decentraland.Common;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent
    {
        public bool IsDirty { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ICustomTweener CustomTweener { get; set; }

        public PBTween CachedTween { get; set; } 

        public bool IsActive()
        {
            return CustomTweener != null && CustomTweener.IsActive();
        }

        public void Rewind()
        {
            CustomTweener.Pause();
            CustomTweener.Rewind();
        }


        public void CopyToCacheTween(PBTween pbTween)
        {
            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.None:
                    break;
                case PBTween.ModeOneofCase.Move:
                    CachedTween.Move = new Move();
                    CachedTween.Move.Start = new Vector3();
                    CachedTween.Move.End = new Vector3();
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    CachedTween.Rotate = new Rotate();
                    CachedTween.Rotate.Start = new Quaternion();
                    CachedTween.Rotate.End = new Quaternion();
                    break;
                case PBTween.ModeOneofCase.Scale:
                    CachedTween.Scale = new Scale();
                    CachedTween.Scale.Start = new Vector3();
                    CachedTween.Scale.End = new Vector3();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            CachedTween.MergeFrom(pbTween);
        }
    }
    
}
