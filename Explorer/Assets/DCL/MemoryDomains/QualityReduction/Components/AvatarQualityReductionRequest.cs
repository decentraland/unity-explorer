using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct AvatarQualityReductionRequest
{
    public bool Reduce;

    public AvatarQualityReductionRequest(bool reduce)
    {
        Reduce = reduce;
    }
}

public struct AvatarQualityReduced
{
    
}