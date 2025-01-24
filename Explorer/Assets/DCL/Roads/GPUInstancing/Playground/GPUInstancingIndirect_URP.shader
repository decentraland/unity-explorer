Shader "DCL/Debug/GPUInstancingIndirect_URP"
{
   SubShader
   {
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
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

           struct PerInstanceBufferData
           {
               float4x4 instMatrix;
               float4   instColourTint;
           };

           StructuredBuffer<PerInstanceBufferData> _PerInstanceBuffer;
           uint _StartInstance;

           struct Attributes
           {
               float3 positionOS : POSITION;
               uint   instanceID : SV_InstanceID;
           };

           struct Varyings
           {
               float4 positionHCS : SV_Position;
               float4 color       : COLOR;
           };

           Varyings vert(Attributes IN)
           {
               Varyings OUT;
               
               uint bufferIndex = IN.instanceID + _StartInstance;
               PerInstanceBufferData data = _PerInstanceBuffer[bufferIndex];

               float3 worldPos = mul(data.instMatrix, float4(IN.positionOS, 1.0)).xyz;
               OUT.positionHCS = TransformWorldToHClip(worldPos);
               OUT.color = data.instColourTint;

               return OUT;
           }

           half4 frag(Varyings IN) : SV_Target
           {
               return IN.color;
           }
           ENDHLSL
       }
   }
}