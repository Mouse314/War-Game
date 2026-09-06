Shader "Custom/ParticlesShader"
{

    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white"
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct ParticleData
            {
                float3 position;
                float3 velocity;
                float4 color;
            };
            StructuredBuffer<ParticleData> _particlesData;

            float glow = 1.0;
            float particleSize = 0.02;
            static const float3x3 meshRotationOffset = float3x3(
                1, 0, 0,
                0, 1, 0,
                0, 0, 1
            );

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                nointerpolation float instanceID : TEXCOORD1;
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                ParticleData particleData = _particlesData[IN.instanceID];
                float3 particlePosition = particleData.position;
                float3 particleVelocity = particleData.velocity;
                float4 particleColor = particleData.color;

                // Enemy size 3x
                // if (IN.instanceID == 1000) {
                //     particleSize *= 10.0;
                // }

                float3 worldPosition = particlePosition + IN.positionOS * particleSize;

                // Alignment with velocity
                float3 velocity = particleVelocity;
                float3 localPosition = mul(meshRotationOffset, IN.positionOS * particleSize);
                float3 localNormal = mul(meshRotationOffset, IN.normalOS);
                float3 alignedNormal = localNormal;

                if (length(velocity) > 0.001)
                {
                    float3 forward = normalize(velocity);
                    float3 up = float3(0, 1, 0);
                    float3 right = cross(up, forward);
                    if (dot(right, right) < 0.0001)
                    {
                        up = float3(0, 0, 1);
                        right = cross(up, forward);
                    }
                    right = normalize(right);
                    up = cross(forward, right);
                    up = normalize(up);

                    float4x4 rotationMatrix = float4x4(
                        float4(right.x, up.x, forward.x, 0),
                        float4(right.y, up.y, forward.y, 0),
                        float4(right.z, up.z, forward.z, 0),
                        float4(0, 0, 0, 1)
                    );

                    worldPosition = mul(rotationMatrix, float4(localPosition, 1)).xyz + particlePosition;
                    alignedNormal = mul(rotationMatrix, float4(localNormal, 0)).xyz;
                }
                else
                {
                    worldPosition = particlePosition + localPosition;
                }

                OUT.instanceID = (float)IN.instanceID;
                OUT.positionHCS = TransformWorldToHClip(worldPosition);
                OUT.normalWS = TransformObjectToWorldNormal(alignedNormal);
                OUT.color = particleColor * _BaseColor;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 lighting = ambient + mainLight.color * diffuse;

                // Enemy are red
                // if (IN.instanceID == 1000.0) {
                //     _BaseColor.rgb = float3(1, 0, 0); // Red
                // }

                float4 color = IN.color;

                return half4(IN.color * lighting * glow, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
