using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine.Rendering.Universal;

namespace DCL.Quality
{
    public static class URPReflection
    {
        // Delegate types for the fields access
        public delegate ScriptableRendererData[] GetRendererDataListDelegate(UniversalRenderPipelineAsset asset);

        public delegate int GetDefaultRendererIndexDelegate(UniversalRenderPipelineAsset asset);

        public static GetRendererDataListDelegate CreateGetRendererDataListDelegate()
        {
            FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            ParameterExpression target = Expression.Parameter(typeof(UniversalRenderPipelineAsset), "asset");
            MemberExpression fieldAccess = Expression.Field(target, field);
            var lambda = Expression.Lambda<GetRendererDataListDelegate>(fieldAccess, target);
            return lambda.Compile();
        }

        public static GetDefaultRendererIndexDelegate CreateGetDefaultRendererIndexDelegate()
        {
            FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            ParameterExpression target = Expression.Parameter(typeof(UniversalRenderPipelineAsset), "asset");
            MemberExpression fieldAccess = Expression.Field(target, field);
            var lambda = Expression.Lambda<GetDefaultRendererIndexDelegate>(fieldAccess, target);
            return lambda.Compile();
        }

        public static readonly GetRendererDataListDelegate GetRendererDataList = CreateGetRendererDataListDelegate();
        public static readonly GetDefaultRendererIndexDelegate GetDefaultRendererIndex = CreateGetDefaultRendererIndexDelegate();
    }
}
