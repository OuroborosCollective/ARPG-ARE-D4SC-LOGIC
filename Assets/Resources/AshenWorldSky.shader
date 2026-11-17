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

                float moonAngle = saturate(dot(direction, -normalize(_SunDirection.xyz)));
                float moon = smoothstep(0.9991, 0.9998, moonAngle) * (1.0 - _Daylight);
                sky += half3(0.28, 0.36, 0.54) * moon;

                if (_CloudAmount > 0.001)
                {
                    float cloudShape = sin(direction.x * 19.0 + direction.z * 7.0 + sin(direction.y * 11.0) + _CloudPhase);
                    cloudShape += sin(direction.z * 27.0 - direction.x * 9.0 + sin(direction.y * 14.0) - _CloudPhase);
                    float cloud = smoothstep(0.55, 1.55, cloudShape)
                                  * smoothstep(-0.05, 0.28, direction.y) * _CloudAmount;
                    sky = lerp(sky, _CloudColor.rgb * (0.16 + 0.77 * _Daylight), saturate(cloud * 0.63));
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
