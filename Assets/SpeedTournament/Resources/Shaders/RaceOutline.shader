Shader "SpeedTournament/RaceOutline"
{
    Properties { _BaseColor("Color", Color)=(0.012,0.017,0.035,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            float4 vert(float4 pos:POSITION):SV_POSITION { return TransformObjectToHClip(pos.xyz*1.09); }
            half4 frag():SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
