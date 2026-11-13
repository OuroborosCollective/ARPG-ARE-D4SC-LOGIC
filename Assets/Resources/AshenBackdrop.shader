Shader "AshenCrypt/Backdrop"
{
    Properties
    {
        _ZWrite ("Depth write", Float) = 1
        _FogStrength ("Fog", Range(0,1)) = 1
        _BaseMap ("Ground texture", 2D) = "white" {}
        _TextureStrength ("Ground texture strength", Range(0,1)) = 0
    }
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
                float _TextureStrength;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct In { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Out { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; float2 ground : TEXCOORD1; };
            Out vert(In i)
            {
                Out o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color; o.fog = ComputeFogFactor(o.positionCS.z);
                o.ground = TransformObjectToWorld(i.positionOS.xyz).xz; return o;
            }
            half4 frag(Out i) : SV_Target
            {
                float distance = max(abs(i.ground.x), abs(i.ground.y));
                half textureWeight = (1 - smoothstep(30, 95, distance)) * _TextureStrength;
                half3 soil = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.ground * .4).rgb * .35;
                half3 color = lerp(i.color.rgb, soil, textureWeight);
                return half4(lerp(color, MixFog(color, i.fog), _FogStrength), 1);
            }
            ENDHLSL
        }
    }
}
