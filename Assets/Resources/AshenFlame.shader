Shader "AshenCrypt/Flame" {
 Properties { _Intensity ("Flame brightness", Range(0,2)) = 1 _Seed ("Per-torch phase", Float) = 0 }
 SubShader {
  Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
  Blend One One
  ZWrite Off
  Cull Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half _Intensity; float _Seed;
   CBUFFER_END
   struct In { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
   struct Out { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; };
   // The quad always faces the camera around the world up axis: no per-frame script work.
   Out vert(In i) {
    Out o;
    float3 origin = TransformObjectToWorld(float3(0,0,0));
    float3 toCamera = _WorldSpaceCameraPos - origin; toCamera.y = 0;
    float len = max(length(toCamera), 1e-4);
    float3 right = float3(toCamera.z, 0, -toCamera.x) / len;
    float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), 1);
    float phase = _Time.y * 1.7 + _Seed;
    // The tip sways, the base stays on the wick.
    float sway = sin(phase * 2.3 + i.positionOS.y * 3.0) * 0.06 * i.uv.y * i.uv.y;
    float3 world = origin + right * (i.positionOS.x * scale.x + sway) + float3(0, i.positionOS.y * scale.y, 0);
    o.positionCS = TransformWorldToHClip(world); o.uv = i.uv; o.phase = phase;
    return o;
   }
   half hash(half2 p) { return frac(sin(dot(p, half2(127.1h, 311.7h))) * 43758.5453h); }
   half noise(half2 p) {
    half2 c = floor(p); half2 f = frac(p); f = f * f * (3.0h - 2.0h * f);
    return lerp(lerp(hash(c), hash(c + half2(1,0)), f.x), lerp(hash(c + half2(0,1)), hash(c + half2(1,1)), f.x), f.y);
   }
   half4 frag(Out i) : SV_Target {
    half2 uv = i.uv;
    // Two cheap octaves scrolling upward tear the silhouette into licking tongues.
    half n = noise(half2(uv.x * 4.0h, uv.y * 3.0h - i.phase * 1.6h)) * 0.65h
           + noise(half2(uv.x * 9.0h + 3.7h, uv.y * 6.0h - i.phase * 2.7h)) * 0.35h;
    half x = abs(uv.x - 0.5h + (n - 0.5h) * 0.22h * uv.y) * 2.0h;
    half width = lerp(0.50h, 0.03h, saturate(uv.y * uv.y * 0.7h + uv.y * 0.3h));
    half body = saturate(1.0h - x / max(width, 0.02h));
    half lift = saturate(1.0h - uv.y - (1.0h - n) * 0.35h * uv.y);
    half heat = body * body * lift * smoothstep(0.0h, 0.08h, uv.y) * (0.75h + 0.5h * n);
    // White-yellow core, orange body, deep red edge.
    half3 colour = lerp(half3(0.85h, 0.13h, 0.02h), half3(1.0h, 0.55h, 0.12h), saturate(heat * 2.2h));
    colour = lerp(colour, half3(1.0h, 0.93h, 0.68h), saturate(heat * 2.6h - 1.1h));
    // A soft halo sells the light source against a dark sky without a bloom pass.
    half2 away = (uv - half2(0.5h, 0.40h)) * half2(2.1h, 2.4h);
    half halo = saturate(1.0h - dot(away, away)); halo *= halo * halo;
    half3 light = colour * heat * 1.9h + half3(1.0h, 0.46h, 0.13h) * halo * (0.20h + 0.06h * n);
    return half4(light * _Intensity, 1);
   }
   ENDHLSL
  }
 }
}
