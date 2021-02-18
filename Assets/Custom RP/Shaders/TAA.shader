Shader "Unlit/TAA"
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex TAAVertex
            #pragma fragment TAAFragment

            #include "../ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _HistoryBuffer;
            float _BlendAlpha;
            float4x4 previousViewPojection;
            float2 previousOffset;
            float2 currentOffset;
            float2 screenSize;

            float2 reprojection(float3 positionWS)
            {
                float4 positionCS = mul(previousViewPojection, float4(positionWS.xyz), 1.0);
                positionCS /= positionCS.w;
                float2 screenPos = positionCS * 0.5 + 0.5;
                screenPos = screenPos - previousOffset + currentOffset;
                return screenPos;
            }

            Varyings TAAVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 TAAFragment (Varyings input) : SV_Target
            {
                float4 history = tex2D(_HistoryBuffer, input.uv);
                float4 current = tex2D(_MainTex, input.uv);
                return lerp(history, current, _BlendAlpha);
            }
            ENDHLSL
        }
    }
}
