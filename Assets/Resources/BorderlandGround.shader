Shader "ARE/World/Schneeasche"
{
    Properties
    {
        [NoScaleOffset] _Mask ("Asche, Ablagerung, Glutriss", 2D) = "gray" {}
        _Metres ("Kachelbreite in Metern", Float) = 8.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Schneeasche"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ ARE_BORDERLAND_PAINT
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_AREAshPainting);
            TEXTURE2D(_AREAshVeins);
            SAMPLER(sampler_AREAshPainting);
            CBUFFER_START(UnityPerMaterial)
                float _Metres;
            CBUFFER_END
            // The existing Core solar phase and the accessibility preference own these two inputs.
            float _AREEmberGain;
            struct Attributes { float4 positionOS : POSITION; half3 normalOS : NORMAL; };
            struct Varyings { float4 clip : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half fog : TEXCOORD2; float4 shadow : TEXCOORD3; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.clip = p.positionCS;
                o.world = p.positionWS;
                o.normal = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.shadow = GetShadowCoord(p);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.world.xz / _Metres;
                half3 ground = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).rgb;
                #if defined(ARE_BORDERLAND_PAINT)
                    // The director's 57 m square has 3.5625 repeats. Local meshes, outer relief
                    // and the original village backdrop agree on the same 16 m world period.
                    float2 paintedUV = (i.world.xz + 28.5) / 16.0;
                    half3 painting = SAMPLE_TEXTURE2D(_AREAshPainting, sampler_AREAshPainting, paintedUV).rgb;
                    half vein = SAMPLE_TEXTURE2D(_AREAshVeins, sampler_AREAshPainting, paintedUV).r;
                    half value = dot(painting, half3(.24,.64,.12));
                    half3 albedo = lerp(half3(.064,.070,.076), half3(.71,.74,.75),
                                        smoothstep(.095h,.82h,value)) * (.83h + .31h * ground.r);
                    half core = smoothstep(.37h,.93h,vein);
                #else
                    float2 wind = float2(.81 * uv.x - .53 * uv.y, .53 * uv.x + .81 * uv.y) * .24;
                    half drift = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, wind + .37).g;
                    half ash = smoothstep(.37h,.76h, ground.g * .70h + drift * .30h);
                    half core = ground.b * smoothstep(.26h,.60h,drift) * (1.0h - .70h * ash);
                    half3 basalt = lerp(half3(.060,.060,.063), half3(.216,.215,.207), ground.r);
                    half3 powder = lerp(half3(.39,.42,.44), half3(.69,.71,.71), ground.r);
                    half3 albedo = lerp(basalt, powder, ash);
                #endif
                albedo *= 1.0h - .54h * core;
                half3 n = normalize(i.normal);
                Light key = GetMainLight(i.shadow);
                half3 sky = max(half3(.08,.095,.12), SampleSH(n));
                half3 illumination = sky + key.color * (saturate(dot(n, key.direction)) * key.shadowAttenuation);
                half3 ember = half3(.93,.133,.033) * core * _AREEmberGain;
                return half4(MixFog(albedo * illumination + ember, i.fog), 1.0);
            }
            ENDHLSL
        }
    }
}
