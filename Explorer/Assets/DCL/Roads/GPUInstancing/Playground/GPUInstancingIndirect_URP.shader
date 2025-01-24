Shader "DCL/Debug/GPUInstancingIndirect_URP"
{
    SubShader
    {
        // Basic tags for URP's opaque forward pass
        Tags
        {
            "RenderType" = "Opaque"
            "Queue"      = "Geometry"
            "LightMode"  = "UniversalForward"
        }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP core library for transformation helpers
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //--------------------------------------------------
            // Match your C# struct exactly (same field order):
            //--------------------------------------------------
            struct PerInstanceBufferData
            {
                float4x4 instMatrix;     // local->world
                float4   instColourTint; // RGBA color
            };

            // The StructuredBuffer that we'll bind in C# with matProps.SetBuffer("_PerInstanceBuffer", instanceBuffer).
            StructuredBuffer<PerInstanceBufferData> _PerInstanceBuffer;

            // Vertex input
            struct Attributes
            {
                float3 positionOS : POSITION;
                uint   instanceID : SV_InstanceID;
            };

            // Vertex -> Fragment
            struct Varyings
            {
                float4 positionHCS : SV_Position;
                float4 color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 1) Per-instance data
                PerInstanceBufferData data = _PerInstanceBuffer[IN.instanceID];

                // 2) local -> world
                float3 worldPos = mul(data.instMatrix, float4(IN.positionOS, 1.0)).xyz;

                // 3) world -> clip
                // TransformWorldToHClip is a URP helper that internally uses unity_MatrixVP
                OUT.positionHCS = TransformWorldToHClip(worldPos);

                // 4) Pass the color
                OUT.color = data.instColourTint;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color; // unlit color from the buffer
            }
            ENDHLSL
        }
    }
}