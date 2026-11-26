Shader "AshenCrypt/Weltferne"
{
    Properties
    {
        _masonry ("Weathered masonry", 2D) = "white" {}
        _rock ("Stratified rock", 2D) = "white" {}
        _timber ("Aged timber", 2D) = "white" {}
        _earth ("Ash and scree", 2D) = "white" {}
        _HasDetail ("Detail maps available", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+499" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_masonry); SAMPLER(sampler_masonry);
            TEXTURE2D(_rock);    SAMPLER(sampler_rock);
            TEXTURE2D(_timber);  SAMPLER(sampler_timber);
            TEXTURE2D(_earth);   SAMPLER(sampler_earth);
            CBUFFER_START(UnityPerMaterial)
                half _HasDetail;
            CBUFFER_END

            // The existing world-sky clock publishes alpha one outdoors, zero under authored light.
            half4 _AshenWorldTint;
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 original : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                // Preserve the photographed angle and internal depth ordering. Occupy the far
                // part of the camera's depth buffer so playable buildings and gates occlude the
                // horizon, including on the largest forest maps. UNITY_MATRIX_P handles reversed Z.
                float depth = max(0.0, -TransformWorldToView(world).z);
                float distant = _ProjectionParams.z * lerp(.60, .94, depth / (depth + 110.0));
                float4 projected = mul(UNITY_MATRIX_P, float4(0.0, 0.0, -distant, 1.0));
                output.positionCS.z = (projected.z / projected.w) * output.positionCS.w;
                output.original = input.positionOS;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = input.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                int surface = (int)round(input.color.a * 255.0h);
                float3 dx = ddx(input.original), dy = ddy(input.original);
                float3 face = abs(cross(dx,dy));
                bool floorFace = face.y > .71 * length(face);
                bool sideFace = face.x > face.z;
                float2 uv = floorFace ? input.original.xz : (sideFace ? input.original.zy : input.original.xy);
                float2 ux = floorFace ? dx.xz : (sideFace ? dx.zy : dx.xy);
                float2 uy = floorFace ? dy.xz : (sideFace ? dy.zy : dy.xy);
                if (_HasDetail > 0.5h && surface > 0 && surface <= 4)
                {
                    float tile = surface == 1 ? 3.0 : (surface == 2 ? 5.0 : (surface == 3 ? 2.5 : 4.0));
                    uv /= tile; ux /= tile; uy /= tile;
                    half grain;
                    UNITY_BRANCH if (surface == 1) grain = SAMPLE_TEXTURE2D_GRAD(_masonry,sampler_masonry,uv,ux,uy).r;
                    else if (surface == 2) grain = SAMPLE_TEXTURE2D_GRAD(_rock,sampler_rock,uv,ux,uy).r;
                    else if (surface == 3) grain = SAMPLE_TEXTURE2D_GRAD(_timber,sampler_timber,uv,ux,uy).r;
                    else grain = SAMPLE_TEXTURE2D_GRAD(_earth,sampler_earth,uv,ux,uy).r;
                    color *= grain;
                }
                color *= lerp(half3(1,1,1), _AshenWorldTint.rgb, saturate(_AshenWorldTint.a));
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
