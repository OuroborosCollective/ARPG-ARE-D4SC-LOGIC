Shader "AshenCrypt/Art/Crusted Lava"
{
    Properties
    {
        _Crust ("Basalt RGB, fissures alpha", 2D) = "gray" {}
        [Normal] _BumpMap ("Basalt normals", 2D) = "bump" {}
        _Heat ("Visible heat", Range(0,2)) = 1
        _Phase ("Visual phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Crust); SAMPLER(sampler_Crust);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _Crust_ST;
                half _Heat;
                float _Phase;
            CBUFFER_END
            struct App {float4 pos:POSITION; half3 n:NORMAL; half4 t:TANGENT; float2 uv:TEXCOORD0;};
            struct Frag {float4 clip:SV_POSITION; float2 uv:TEXCOORD0; half3 n:TEXCOORD1; half4 t:TEXCOORD2; half fog:TEXCOORD3; float3 world:TEXCOORD4;};
            Frag vert(App i)
            {
                Frag o;
                VertexPositionInputs p=GetVertexPositionInputs(i.pos.xyz);
                o.clip=p.positionCS; o.world=p.positionWS; o.uv=TRANSFORM_TEX(i.uv,_Crust);
                o.n=TransformObjectToWorldNormal(i.n); o.t=half4(TransformObjectToWorldDir(i.t.xyz),i.t.w*GetOddNegativeScale());
                o.fog=ComputeFogFactor(o.clip.z); return o;
            }
            half4 frag(Frag i):SV_Target
            {
                half4 crust=SAMPLE_TEXTURE2D(_Crust,sampler_Crust,i.uv);
                half3 bump=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv));
                half3 n=normalize(i.n); half3 t=normalize(i.t.xyz); half3 b=cross(n,t)*i.t.w;
                n=normalize(t*bump.x+b*bump.y+n*bump.z);
                Light sun=GetMainLight();
                half3 base=crust.rgb*(max(SampleSH(n),half3(.13h,.14h,.16h))+sun.color*saturate(dot(n,sun.direction)));
                half pulse=.91h+.09h*sin(_Phase+dot(i.world.xz,float2(2.1,1.3)));
                half core=smoothstep(.29h,.81h,crust.a);
                half3 light=lerp(half3(.62h,.032h,.004h),half3(2.05h,.50h,.055h),core);
                return half4(MixFog(base+light*crust.a*_Heat*pulse,i.fog),1);
            }
            ENDHLSL
        }
    }
}
