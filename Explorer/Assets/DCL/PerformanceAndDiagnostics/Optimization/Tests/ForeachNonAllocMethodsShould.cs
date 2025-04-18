using DCL.Optimization.Iterations;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace DCL.PerformanceAndDiagnostics.Optimization.Tests
{
    [TestFixture]
    public class ForeachNonAllocMethodsShould
    {
        private static readonly object[] SOURCE_LISTS =
        {
            new object[] { new List<TestValue> { 1, 100, 510525, 131, 51252, 0x50, 100 } },
            new object[] { new List<TestValue> { 411, 1527, 654298, 752, 5125, 5512, 878953 } },
        };

        [Test]
        [TestCaseSource(nameof(SOURCE_LISTS))]
        public void SameBehaviour(List<TestValue> list)
        {
            var alloc = list.Select(e => new TestValue(e.value)).ToList();
            var nonAlloc = list.Select(e => new TestValue(e.value)).ToList();

            int increment = Random.Range(10, 100);

            foreach (TestValue i in alloc) i.value += increment;

            nonAlloc.ForeachNonAlloc(increment, static (increment, i) => i.value += increment);

            CollectionAssert.AreEquivalent(alloc, nonAlloc);
        }

        public class TestValue : IComparable<TestValue>, IEquatable<TestValue>
        {
            public int value;

            public TestValue(int value)
            {
                this.value = value;
            }

            public int CompareTo(TestValue other) =>
                value.CompareTo(other.value);

            public bool Equals(TestValue other)
            {
                if (ReferenceEquals(null, other)) return false;
                return value == other.value;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TestValue)obj);
            }

            public override int GetHashCode() =>
                value;

            public static implicit operator TestValue(int value) =>
                new (value);

            public override string ToString() =>
                value.ToString();
        }
    }
}
