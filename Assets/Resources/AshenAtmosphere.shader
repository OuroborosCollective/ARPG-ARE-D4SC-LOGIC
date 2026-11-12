Shader "AshenCrypt/Atmosphere" {
 Properties { _MainTex ("Particle", 2D) = "white" {} }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off
  Cull Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   struct In { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
   struct Out { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
   Out vert(In i) { Out o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.color=i.color; o.uv=i.uv; return o; }
   half4 frag(Out i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color; }
   ENDHLSL
  }
 }
}
