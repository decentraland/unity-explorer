using System;

namespace Utility.Ownership
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class TakesOwnershipAttribute : Attribute
    {

    }
}
