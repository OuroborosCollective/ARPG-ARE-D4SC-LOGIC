Shader "ARE/World/Schneeasche"
{
    Properties
    {
        [NoScaleOffset] _Mask ("Asche, Ablagerung, Glutriss", 2D) = "gray" {}
        _VillageEarth ("Getretene Dorferde", 2D) = "gray" {}
        _Metres ("Kachelbreite in Metern", Float) = 5.2
        _StoneTint ("Schieferton", Color) = (1,1,1,1)
        [HideInInspector] _Cull ("Cull", Float) = 2
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
            #pragma shader_feature_local _ ARE_CLEFT_STONE ARE_VILLAGE_EARTH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURE2D(_VillageEarth);
            SAMPLER(sampler_VillageEarth);
            TEXTURE2D(_AREAshPainting);
            TEXTURE2D(_AREAshVeins);
            SAMPLER(sampler_AREAshPainting);
            CBUFFER_START(UnityPerMaterial)
                float _Metres;
                half4 _StoneTint;
                float4 _VillageEarth_ST;
            CBUFFER_END
            // The existing Core solar phase and the accessibility preference own these two inputs.
            float _AREEmberGain;
            struct Attributes { float4 positionOS : POSITION; half3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 clip : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1;
                half fog : TEXCOORD2; float4 shadow : TEXCOORD3; half3 vertexLights : TEXCOORD4;
                #if defined(ARE_VILLAGE_EARTH)
                    float2 sourceUV : TEXCOORD5;
                #endif
            };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.clip = p.positionCS;
                o.world = p.positionWS;
                o.normal = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.shadow = GetShadowCoord(p);
                o.vertexLights = VertexLighting(p.positionWS, o.normal);
                #if defined(ARE_VILLAGE_EARTH)
                    o.sourceUV = TRANSFORM_TEX(i.uv, _VillageEarth);
                #endif
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.world.xz / _Metres;
                half3 n = normalize(i.normal);
                half3 ground = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv).rgb;
                half3 slate = lerp(half3(.100,.116,.140), half3(.46,.50,.53), ground.r);
                half3 powder = lerp(half3(.38,.415,.45), half3(.75,.775,.79), ground.r);
                #if defined(ARE_VILLAGE_EARTH)
                    half3 earth = SAMPLE_TEXTURE2D(_VillageEarth, sampler_VillageEarth, i.sourceUV).rgb;
                    half grain = dot(earth, half3(.24,.64,.12));
                    earth = lerp(grain * half3(.69,.62,.55), earth, .52h) * (.68h + .32h * ground.r);
                    half drift = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv * .19 + float2(.33,.71)).g;
                    // The road to the north gate and Mara's supply court wear through to the original soil.
                    half roadside = smoothstep(1.1h, 7.0h, abs(i.world.x));
                    half court = smoothstep(.42h, 1.55h, length((i.world.xz - float2(-6.8,-10.5)) * float2(.23,.29)));
                    half cover = .20h + .53h * roadside + .30h * (ground.g-.5h) + .63h * (drift-.5h);
                    cover = smoothstep(.06h,.84h, cover * (.72h + .28h * court));
                    half3 albedo = lerp(earth, lerp(slate, powder, .59h + .34h * ground.g), cover);
                    half core = 0;
                #elif defined(ARE_CLEFT_STONE)
                    float2 edgeUV = float2(abs(n.x) > abs(n.z) ? i.world.z : i.world.x, i.world.y) / _Metres;
                    half face = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, edgeUV).r;
                    half cap = smoothstep(.32h,.86h,n.y);
                    half3 rock = lerp(half3(.043,.054,.069), half3(.365,.392,.425), face);
                    half3 albedo = lerp(rock, lerp(slate,powder,.56h + .36h*ground.g),cap) * _StoneTint.rgb;
                    half core = 0;
                #elif defined(ARE_BORDERLAND_PAINT)
                    // The director's 57 m square has 3.5625 repeats. Local meshes, outer relief
                    // and the original village backdrop agree on the same 16 m world period.
                    float2 paintedUV = (i.world.xz + 28.5) / 16.0;
                    half3 painting = SAMPLE_TEXTURE2D(_AREAshPainting, sampler_AREAshPainting, paintedUV).rgb;
                    half vein = SAMPLE_TEXTURE2D(_AREAshVeins, sampler_AREAshPainting, paintedUV).r;
                    half value = dot(painting, half3(.24,.64,.12));
                    half deposit = smoothstep(.09h,.80h,value) * (.80h + .18h*ground.g);
                    half3 albedo = lerp(slate,powder,deposit);
                    half core = smoothstep(.48h,.89h,vein) * smoothstep(.29h,.64h,ground.g);
                #else
                    float2 wind = float2(.81 * uv.x - .53 * uv.y, .53 * uv.x + .81 * uv.y) * .24;
                    half drift = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, wind + .37).g;
                    half ash = smoothstep(.37h,.76h, ground.g * .70h + drift * .30h);
                    half core = ground.b * smoothstep(.26h,.60h,drift) * (1.0h - .70h * ash);
                    half3 albedo = lerp(slate, powder, ash);
                #endif
                albedo *= 1.0h - .54h * core;
                Light key = GetMainLight(i.shadow);
                half3 sky = max(half3(.08,.095,.12), SampleSH(n));
                half3 illumination = sky + key.color * (saturate(dot(n, key.direction)) * key.shadowAttenuation);
                half neutral = dot(illumination,half3(.2126,.7152,.0722));
                illumination = lerp(illumination, neutral*half3(.96,1.0,1.04), .64h);
                half3 localLight = i.vertexLights;
                #if defined(_ADDITIONAL_LIGHTS)
                    // Two existing local lamps per fragment on the mobile forward renderer.
                    uint count = min((uint)GetAdditionalLightsCount(), 2u);
                    for (uint index = 0u; index < count; index++)
                    {
                        Light lamp = GetAdditionalLight(index,i.world);
                        localLight += lamp.color * (lamp.distanceAttenuation * saturate(dot(n,lamp.direction)));
                    }
                #endif
                half3 ember = half3(.96,.109,.024) * core * _AREEmberGain;
                return half4(MixFog(albedo * (illumination + localLight) + ember, i.fog), 1.0);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Simple Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Simple Lit/DepthOnly"
    }
}
