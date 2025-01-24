Shader "DCL/IndirectInstancingExample"
{
     SubShader
    {
        // Basic pass, no specific tags required for a simple unlit debug
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // If you want standard includes:
            #include "UnityCG.cginc"

            //-------------------------------------------
            // Must match your C# struct exactly
            //-------------------------------------------
            struct PerInstanceBufferData
            {
                float4x4 instMatrix;     // local->world
                float4   instColourTint; // RGBA color
            };

            // StructuredBuffer that we'll bind in C# with `matProps.SetBuffer("_PerInstanceBuffer", instanceBuffer)`.
            StructuredBuffer<PerInstanceBufferData> _PerInstanceBuffer;

            // Vertex input struct
            struct appdata
            {
                float3 vertex    : POSITION;
                uint   instanceID: SV_InstanceID;  // automatically increments for each instance
            };

            // Vertex to fragment struct
            struct v2f
            {
                float4 positionHCS : SV_POSITION;  // clip-space position
                float4 color       : COLOR;        // pass color to fragment
            };

            v2f vert(appdata IN)
            {
                v2f OUT;

                // 1) Grab the correct instance data for this instance
                PerInstanceBufferData data = _PerInstanceBuffer[IN.instanceID];

                // 2) Transform from local -> instance -> world
                float4 worldPos = mul(data.instMatrix, float4(IN.vertex, 1.0));

                // 3) Multiply by the built-in UNITY_MATRIX_VP (view-projection)
                OUT.positionHCS = mul(UNITY_MATRIX_VP, worldPos);

                // 4) Pass the color to the fragment
                OUT.color = data.instColourTint;

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                // Output the per-instance color
                return IN.color;
            }
            ENDHLSL
        }
    }
}