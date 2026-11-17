Shader "AshenCrypt/WorldSky"
{
    Properties
    {
        _ZenithColor ("Day zenith", Color) = (0.20,0.40,0.62,1)
        _HorizonColor ("Day horizon", Color) = (0.62,0.67,0.66,1)
        _NightColor ("Night sky", Color) = (0.017,0.027,0.065,1)
        _CloudColor ("Clouds", Color) = (0.82,0.82,0.81,1)
        _SunColor ("Sun and twilight", Color) = (1.0,0.67,0.31,1)
        _SunDirection ("Sun direction", Vector) = (0,1,0,0)
        _Daylight ("Daylight", Range(0,1)) = 1
        _CloudAmount ("Cloud amount", Range(0,1)) = 0.75
        _CloudPhase ("Persisted cloud phase", Float) = 0
        _StarAmount ("Star amount", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _NightColor;
                half4 _CloudColor;
                half4 _SunColor;
                float4 _SunDirection;
                half _Daylight;
                half _CloudAmount;
                float _CloudPhase;
                half _StarAmount;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.direction = input.positionOS;
                return output;
            }
            half skyHash(half2 p) { return frac(sin(dot(p, half2(127.1h, 311.7h))) * 43758.5453h); }
            half skyNoise(half2 p)
            {
                half2 c = floor(p); half2 f = frac(p); f = f * f * (3.0h - 2.0h * f);
                return lerp(lerp(skyHash(c), skyHash(c + half2(1,0)), f.x),
                            lerp(skyHash(c + half2(0,1)), skyHash(c + half2(1,1)), f.x), f.y);
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float up = saturate(direction.y * 0.82 + 0.18);
                float horizon = smoothstep(0.06, 0.84, up);
                half3 day = lerp(_HorizonColor.rgb, _ZenithColor.rgb, horizon);
                half3 sky = lerp(_NightColor.rgb, day, _Daylight);

                float sunAngle = saturate(dot(direction, normalize(_SunDirection.xyz)));
                float sunCore = smoothstep(0.9975, 0.9997, sunAngle) * _Daylight;
                float sunHalo = smoothstep(0.90, 0.995, sunAngle) * _Daylight * (1.0 - sunCore);
                sky += _SunColor.rgb * (sunCore * 1.15 + sunHalo * 0.14);
                // Dawn and dusk: a warm band along the horizon, strongest toward the sun.
                half twilight = saturate(_Daylight * (1.0h - _Daylight) * 4.0h);
                half band = pow(saturate(1.0h - abs(direction.y)), 5.0h);
                sky += _SunColor.rgb * twilight * band * (0.18h + 0.42h * sunAngle * sunAngle);

                float moonAngle = saturate(dot(direction, -normalize(_SunDirection.xyz)));
                float moon = smoothstep(0.9989, 0.9996, moonAngle) * (1.0 - _Daylight);
                float moonHalo = smoothstep(0.955, 0.9995, moonAngle) * (1.0 - _Daylight) * (1.0 - moon);
                sky += half3(0.62, 0.70, 0.86) * moon + half3(0.10, 0.14, 0.24) * moonHalo * moonHalo;

                if (_CloudAmount > 0.001)
                {
                    // A flat cloud deck seen in perspective: clouds shrink and thin toward the horizon.
                    // The persisted phase walks a closed circle, so the deck drifts without a seam at midnight.
                    half2 deck = direction.xz / (max(direction.y, 0.0) + 0.16);
                    half2 drift = half2(cos(_CloudPhase), sin(_CloudPhase)) * 6.0h;
                    half2 p = deck * 0.85h + drift;
                    half shape = skyNoise(p) * 0.56h + skyNoise(p * 2.13h + 7.3h) * 0.29h + skyNoise(p * 4.37h - 3.1h) * 0.15h;
                    half cover = lerp(0.72h, 0.40h, _CloudAmount);
                    half cloud = smoothstep(cover, cover + 0.26h, shape) * smoothstep(0.015, 0.20, direction.y);
                    // Dense cores are shaded, rims catch the sun or the moon; dusk tints the deck.
                    half core = smoothstep(cover + 0.10h, cover + 0.42h, shape);
                    half3 lit = _CloudColor.rgb * (0.055h + 0.905h * _Daylight);
                    half3 shade = lit * lerp(half3(0.50h, 0.54h, 0.64h), half3(0.70h, 0.72h, 0.78h), _Daylight);
                    half3 cloudColor = lerp(lit, shade, core);
                    half twilightCloud = saturate(_Daylight * (1.0h - _Daylight) * 4.0h);
                    cloudColor += _SunColor.rgb * twilightCloud * (0.10h + 0.45h * sunAngle * sunAngle) * (1.0h - core * 0.6h);
                    cloudColor += half3(0.07h, 0.09h, 0.14h) * (1.0h - _Daylight) * moonAngle * moonAngle * (1.0h - core);
                    sky = lerp(sky, cloudColor, cloud * 0.86h);
                }

                if (_StarAmount > 0.001 && _Daylight < 0.80)
                {
                    // Hash only quantized upper-hemisphere direction; stable across frames and saves.
                    float3 cell = floor(direction * 600.0);
                    float seed = frac(sin(dot(cell, float3(17.13, 71.71, 113.91))) * 43758.5453);
                    float star = smoothstep(0.9985, 0.9998, seed)
                               * smoothstep(0.05, 0.45, direction.y)
                               * (1.0 - _Daylight) * _StarAmount;
                    sky += star * half3(0.76, 0.85, 1.0);
                }
                return half4(saturate(sky), 1);
            }
            ENDHLSL
        }
    }
}
