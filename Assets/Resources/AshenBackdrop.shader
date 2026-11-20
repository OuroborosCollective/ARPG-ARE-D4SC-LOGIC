Shader "AshenCrypt/Backdrop"
{
    Properties
    {
        _ZWrite ("Depth write", Float) = 1
        _Exposure ("Presentation daylight", Range(0,1)) = 1
        _FogStrength ("Fog", Range(0,1)) = 1
        _BaseMap ("Ground texture", 2D) = "white" {}
        _TextureStrength ("Ground texture strength", Range(0,1)) = 0
        _GroundMapping ("Ground texture repeats per metre (xy) and shift (zw)", Vector) = (0.4, 0.4, 0, 0)
        _GroundColor ("Light on the ground texture (linear, not a colour swatch)", Vector) = (0.35, 0.35, 0.35, 1)
        _GlowMap ("Ground glow", 2D) = "black" {}
        _GlowStrength ("Ground glow strength", Range(0,4)) = 0
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
                float _Exposure;
                float _TextureStrength;
                float4 _GroundMapping;
                float4 _GroundColor;
                float _GlowStrength;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            TEXTURE2D(_GlowMap);
            SAMPLER(sampler_BaseMap);
            float _AREEmberGain;
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
                half textureWeight = (1 - smoothstep(30, 80, distance)) * _TextureStrength;
                // The land beyond the walked ground continues that ground: same repeat, same place, same brightness.
                float2 uv = i.ground * _GroundMapping.xy + _GroundMapping.zw;
                half3 soil = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _GroundColor.rgb;
                half3 color = lerp(i.color.rgb * _Exposure, soil, textureWeight);   // the ground colour already carries the light
                color += SAMPLE_TEXTURE2D(_GlowMap, sampler_BaseMap, uv).rgb * (_GlowStrength * textureWeight * .55h * _AREEmberGain);
                return half4(lerp(color, MixFog(color, i.fog), _FogStrength), 1);
            }
            ENDHLSL
        }
    }
}
