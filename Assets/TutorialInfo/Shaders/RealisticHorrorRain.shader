Shader "Custom/RealisticHorrorRain"
{
    Properties
    {
        _MainTex ("Rain Streak Texture", 2D) = "white" {}
        _Color ("Rain Tint", Color) = (0.82, 0.86, 0.92, 0.26)
        _CabinExcludeMin ("World Exclude Min (XYZ, W=Enabled)", Vector) = (0, 0, 0, 0)
        _CabinExcludeMax ("World Exclude Max (XYZ, W=Unused)", Vector) = (0, 0, 0, 0)
        _CabinLocalMin ("Local Exclude Min (XYZ, W=Enabled)", Vector) = (0, 0, 0, 0)
        _CabinLocalMax ("Local Exclude Max (XYZ, W=Unused)", Vector) = (0, 0, 0, 0)
        _CanopyExcludeMin ("Canopy World Exclude Min (XYZ, W=Enabled)", Vector) = (0, 0, 0, 0)
        _CanopyExcludeMax ("Canopy World Exclude Max (XYZ, W=Unused)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _CabinExcludeMin;
            float4 _CabinExcludeMax;
            float4 _CabinLocalMin;
            float4 _CabinLocalMax;
            float4 _CanopyExcludeMin;
            float4 _CanopyExcludeMax;
            float4x4 _WorldToCabinMatrix;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Exact local-space cabin interior & roof footprint discard (immune to building rotation/offset)
                if (_CabinLocalMin.w > 0.5)
                {
                    float3 localPos = mul(_WorldToCabinMatrix, float4(i.worldPos, 1.0)).xyz;
                    if (localPos.x > _CabinLocalMin.x && localPos.x < _CabinLocalMax.x &&
                        localPos.y > _CabinLocalMin.y && localPos.y < _CabinLocalMax.y &&
                        localPos.z > _CabinLocalMin.z && localPos.z < _CabinLocalMax.z)
                    {
                        discard;
                    }
                }

                // 2. World-space building interior discard fallback
                if (_CabinExcludeMin.w > 0.5)
                {
                    if (i.worldPos.x > _CabinExcludeMin.x && i.worldPos.x < _CabinExcludeMax.x &&
                        i.worldPos.y > _CabinExcludeMin.y && i.worldPos.y < _CabinExcludeMax.y &&
                        i.worldPos.z > _CabinExcludeMin.z && i.worldPos.z < _CabinExcludeMax.z)
                    {
                        discard;
                    }
                }

                // 3. Secondary world-space exclusion (e.g. Gas Station outdoor pump canopy roof & store overhang)
                if (_CanopyExcludeMin.w > 0.5)
                {
                    if (i.worldPos.x > _CanopyExcludeMin.x && i.worldPos.x < _CanopyExcludeMax.x &&
                        i.worldPos.y > _CanopyExcludeMin.y && i.worldPos.y < _CanopyExcludeMax.y &&
                        i.worldPos.z > _CanopyExcludeMin.z && i.worldPos.z < _CanopyExcludeMax.z)
                    {
                        discard;
                    }
                }

                fixed4 tex = tex2D(_MainTex, i.texcoord);
                fixed4 col = tex * i.color;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
