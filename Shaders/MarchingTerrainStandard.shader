Shader "MarchingTerrain/MarchingTerrainStandard"
{
    Properties
    {
        [HideInInspector]_Layer0("Layer0", 2D) = "white" {}
        [HideInInspector]_Layer1("Layer1", 2D) = "white" {}
        [HideInInspector]_Layer2("Layer2", 2D) = "white" {}
        [HideInInspector]_Layer3("Layer3", 2D) = "white" {}
        [HideInInspector]_Splat("Splat", 3D) = "red" {}
        [HideInInspector]_SplatWidth("SplatWidth", Int) = 128
        [HideInInspector]_SplatHeight("SplatHeight", Int) = 128
        [HideInInspector]_SplatLength("SplatLength", Int) = 128
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Geometry-100"}
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
            float3 vertex;
            float3 worldPos;
            float3 normal;
        };

        sampler2D _Layer0;
        sampler2D _Layer1;
        sampler2D _Layer2;
        sampler2D _Layer3;

        sampler3D _Splat;
        int _SplatWidth;
        int _SplatHeight;
        int _SplatLength;

        const float HalfPI = 1.57079632679;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertex = v.vertex.xyz;
            o.worldPos = mul(unity_ObjectToWorld, v.vertex.xyz);
            o.normal = v.normal;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 splat = tex3D (_Splat, IN.vertex / float3(_SplatWidth, _SplatHeight, _SplatLength));
            fixed4 final = fixed4(0, 0, 0, 0);
            float3 factors;
            factors.x = saturate(dot(float3(1.0, 0.0, 0.0), IN.normal))
                    + saturate(dot(float3(-1.0, 0.0, 0.0), IN.normal));
            factors.y = saturate(dot(float3(0.0, 1.0, 0.0), IN.normal))
                    + saturate(dot(float3(0.0, -1.0, 0.0), IN.normal));
            factors.z = saturate(dot(float3(0.0, 0.0, 1.0), IN.normal))
                    + saturate(dot(float3(0.0, 0.0, -1.0), IN.normal));
            factors /= (factors.x + factors.y + factors.z);
            //Left/Right
            float2 lrUVs = float2(IN.worldPos.z * 0.1, IN.worldPos.y * 0.1);
            if(splat.r > 0)
                final += tex2D(_Layer0, lrUVs) * factors.x * splat.r;
            if(splat.g > 0)
                final += tex2D(_Layer1, lrUVs) * factors.x * splat.g;
            if(splat.b > 0)
                final += tex2D(_Layer2, lrUVs) * factors.x * splat.b;
            if(splat.a > 0)
                final += tex2D(_Layer3, lrUVs) * factors.x * splat.a;
            //Up/Down
            float2 udUVs = float2(IN.worldPos.x * 0.1, IN.worldPos.z * 0.1);
            if(splat.r > 0)
                final += tex2D(_Layer0, udUVs) * factors.y * splat.r;
            if(splat.g > 0)
                final += tex2D(_Layer1, udUVs) * factors.y * splat.g;
            if(splat.b > 0)
                final += tex2D(_Layer2, udUVs) * factors.y * splat.b;
            if(splat.a > 0)
                final += tex2D(_Layer3, udUVs) * factors.y * splat.a;
            //Back/Forward
            float2 bfUVs = float2(IN.worldPos.x * 0.1, IN.worldPos.y * 0.1);
            if(splat.r > 0)
                final += tex2D(_Layer0, bfUVs) * factors.z * splat.r;
            if(splat.g > 0)
                final += tex2D(_Layer1, bfUVs) * factors.z * splat.g;
            if(splat.b > 0)
                final += tex2D(_Layer2, bfUVs) * factors.z * splat.b;
            if(splat.a > 0)
                final += tex2D(_Layer3, bfUVs) * factors.z * splat.a;

            o.Albedo = final;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
