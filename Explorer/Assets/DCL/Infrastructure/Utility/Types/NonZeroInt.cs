using System;
using System.Threading;
using UnityEngine.Assertions;

namespace DCL.Utility.Types
{
    public readonly struct NonZeroInt
    {
        private readonly int value;

        public int Value 
        {
            get
            {
                Assert.AreNotEqual(value, 0);
                return value;
            }
        }

        private NonZeroInt(int value)
        {
            this.value = value;
        }

        public static Result<NonZeroInt> New(int value)
        {
            if (value == 0)
            {
                return Result<NonZeroInt>.ErrorResult("Value must be not 0");
            }
            else
            {
                return Result<NonZeroInt>.SuccessResult(new NonZeroInt(value));
            }
        }
    }
}
