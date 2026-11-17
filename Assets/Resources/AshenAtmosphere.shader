Shader "AshenCrypt/Atmosphere" {
 Properties { _MainTex ("Particle", 2D) = "white" {} _Glow ("Local emissive lift", Range(0,1)) = 0 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  // Premultiplied output: ordinary haze blends exactly as before, glowing embers turn additive.
  Blend One OneMinusSrcAlpha
  ZWrite Off
  Cull Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); half _Glow;
   struct In { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
   struct Out { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
   Out vert(In i) {
    Out o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv;
    // Fade quads that drift into the lens instead of letting large mist sprites pop at the near plane.
    // Vertex-only, no depth texture: affordable on mid-range mobile GPUs.
    half nearFade = lerp(saturate((o.positionCS.w - 0.6) / 1.8), 1.0h, unity_OrthoParams.w);
    o.color = half4(i.color.rgb, i.color.a * nearFade);
    return o;
   }
   half4 frag(Out i) : SV_Target {
    half4 c = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
    half glow = saturate(_Glow);
    // A hot core for embers: brighter where the sprite is dense, less coverage so it adds light.
    half3 rgb = c.rgb * c.a * (1.0h + glow * (0.75h + 1.25h * c.a));
    return half4(rgb, c.a * (1.0h - glow * 0.65h));
   }
   ENDHLSL
  }
 }
}
