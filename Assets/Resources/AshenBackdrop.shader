Shader "AshenCrypt/Backdrop"
{
    Properties { _ZWrite ("Depth write", Float) = 1 _FogStrength ("Fog", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite [_ZWrite]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _FogStrength;
            CBUFFER_END
            struct In { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Out { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; };
            Out vert(In i)
            {
                Out o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color; o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(Out i) : SV_Target
            { return half4(lerp(i.color.rgb, MixFog(i.color.rgb, i.fog), _FogStrength), 1); }
            ENDHLSL
        }
    }
}
