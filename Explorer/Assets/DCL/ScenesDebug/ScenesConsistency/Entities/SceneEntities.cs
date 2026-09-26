using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency.Entities
{
    public class SceneEntities
    {
        public static IEnumerable<SceneEntity> FromText(TextAsset textAsset)
        {
            var entitiesRaw = textAsset.text
                                       .Split('\n')
                                       .Skip(1)
                                       .Where(s => string.IsNullOrWhiteSpace(s) == false);

            return entitiesRaw.Select(x => new SceneEntity(x));
        }
    }
}
