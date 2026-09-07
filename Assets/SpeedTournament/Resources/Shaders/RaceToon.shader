Shader "SpeedTournament/RaceToon"
{
    Properties { _BaseColor("Color", Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i):SV_Target
            {
                half n=dot(normalize(i.normalWS),normalize(float3(-0.4,0.8,-0.5)));
                half shade=n>0.4?1.0:(n> -0.25?0.76:0.48);
                return half4(_BaseColor.rgb*shade,1);
            }
            ENDHLSL
        }
    }
}
