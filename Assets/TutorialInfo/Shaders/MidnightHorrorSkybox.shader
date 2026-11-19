Shader "Skybox/MidnightHorrorSkybox"
{
    Properties
    {
        _ZenithColor ("Midnight Zenith Color", Color) = (0.007, 0.012, 0.026, 1)
        _HorizonColor ("Midnight Horizon Color", Color) = (0.020, 0.029, 0.050, 1)
        _GroundColor ("Midnight Ground Color", Color) = (0.008, 0.011, 0.018, 1)
        _MoonColor ("Moon Color", Color) = (0.84, 0.90, 1.00, 1)
        _MoonDir ("Moon Direction", Vector) = (0.76, 0.46, 0.46, 0)
        _MoonSize ("Moon Angular Radius", Range(0.01, 0.12)) = 0.032
        _StarIntensity ("Star Intensity", Range(0.0, 2.0)) = 0.90
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            fixed4 _MoonColor;
            float4 _MoonDir;
            float _MoonSize;
            float _StarIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                // 1. Deep midnight sky gradient from misty dark blue horizon to near-black indigo zenith
                float upT = saturate(y);
                float skyCurve = pow(upT, 0.55);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, skyCurve);

                // Smoothly blend below horizon into midnight fog/ground
                if (y < 0.0)
                {
                    float downT = saturate(-y * 4.5);
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, downT);
                }

                // 2. Crisp faint midnight stars with subtle analog shimmer
                if (y > 0.03)
                {
                    float3 starGrid = floor(d * 255.0);
                    float3 starCell = frac(d * 255.0) - 0.5;
                    float rnd = hash31(starGrid);
                    if (rnd > 0.9912)
                    {
                        float dist = length(starCell);
                        float starSpot = smoothstep(0.28, 0.03, dist);
                        float horizonFade = smoothstep(0.03, 0.26, y);
                        float twinkle = 0.80 + 0.20 * sin(_Time.y * (1.4 + rnd * 2.8) + rnd * 37.0);
                        float3 starTint = lerp(float3(0.74, 0.84, 1.0), float3(1.0, 0.95, 0.86), frac(rnd * 43.0));
                        col += starTint * (starSpot * horizonFade * twinkle * _StarIntensity * 0.62);
                    }
                }

                // 3. Pale full moon disc & eerie nocturnal halo
                float3 moonDir = normalize(_MoonDir.xyz);
                float moonDot = dot(d, moonDir);
                float cosRadius = cos(_MoonSize);
                float moonDisc = smoothstep(cosRadius - 0.0016, cosRadius + 0.0007, moonDot);

                float crater = 0.86 + 0.14 * hash31(floor(d * 92.0));
                col = lerp(col, _MoonColor.rgb * crater, moonDisc * saturate(y * 8.0));

                float innerHalo = pow(saturate(moonDot), 52.0) * 0.15;
                float outerHalo = pow(saturate(moonDot), 7.5) * 0.032;
                col += _MoonColor.rgb * ((innerHalo + outerHalo) * saturate(y * 2.8 + 0.18));

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}

