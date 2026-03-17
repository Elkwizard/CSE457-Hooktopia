Shader "Lit/Dither"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _Tiling ("Tiling", Float) = 1.0
        _Glossiness ("Smoothness", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "AutoLight.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                SHADOW_COORDS(1)
                float3 normal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float3 coords : TEXCOORD4;
                float4 pos : SV_POSITION;
            };

            float _Tiling;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.texcoord;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.coords = worldPos;
                o.viewDir = UnityWorldSpaceViewDir(worldPos);
                TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 _Color;
            sampler2D _MainTex;
            fixed _Glossiness;

            #define DENSITY 30
            #define LEVELS 5

            static const float kernel[64] = { 0.0,0.5,0.125,0.625,0.03125,0.53125,0.15625,0.65625,0.75,0.25,0.875,0.375,0.78125,0.28125,0.90625,0.40625,0.1875,0.6875,0.0625,0.5625,0.21875,0.71875,0.09375,0.59375,0.9375,0.4375,0.8125,0.3125,0.96875,0.46875,0.84375,0.34375,0.046875,0.546875,0.171875,0.671875,0.015625,0.515625,0.140625,0.640625,0.796875,0.296875,0.921875,0.421875,0.765625,0.265625,0.890625,0.390625,0.234375,0.734375,0.109375,0.609375,0.203125,0.703125,0.078125,0.578125,0.984375,0.484375,0.859375,0.359375,0.953125,0.453125,0.828125,0.328125 };

            float sampleKernel(float2 seed) {
                int2 inx = int2(seed) % 8;
                return kernel[inx.x * 8 + inx.y];
            }

            float2 fract(float2 x) {
                return x % 1;
            }
            
            float random21(float2 seed) {
	            seed = fract((seed + 37.1287) * 9.8612 + seed.x);
	            seed = fract((seed + 47.587) * 7.8212 + seed.y);
	            seed = fract((seed + -921.987) * 2.8612) * 3.91237671;
	            seed = fract((seed + 47.587) * 7.8212 + seed.y);
	            return abs(97.2317 * seed.x + 72.23 * seed.y) % 1;
            }

            float random22(float2 seed) {
	            return float2(
		            random21(seed),
		            random21(seed + float2(87.2, 183.33))
	            );
            }
            fixed dither(fixed v, float2 seed) {
                // return v > sampleKernel(seed);
	            float result = 0.0;
	            for (int i = 0; i < LEVELS; i++) {
		            fixed threshold = random21(seed);
		            seed += v > threshold ? float2(-3.82, 1.12) : float2(3.92, 2.83);
		            result += v > threshold ? 1.0 : 0.0;
	            }
	            return result / LEVELS;
            }
                       
            float2 voronoi(float2 uv, float2 cellSize) {
	            float2 gid = floor(uv / cellSize);
	            float2 local = uv % cellSize / cellSize;

	            float minDist = 10000.0;
	            float2 best = gid;

	            for (int i = -1; i <= 1; i++)
	            for (int j = -1; j <= 1; j++) {
		            float2 off = float2(i, j);
		            float2 p = off + random22(gid + off);
		            float2 diff = p - local;
		            float dist = dot(diff, diff);
		            if (dist < minDist) {
			            minDist = dist;
			            best = gid + off;
		            }
	            }
	            return best;
            }

            fixed3 getAlbedo(float2 uv, float2 pxSize) {
                // return pxSize;
                float2 gid = voronoi(uv, pxSize);//floor(uv / pxSize);
                // float2 guv = (uv % pxSize) / pxSize;
                fixed3 color = tex2D(_MainTex, gid * pxSize).rgb * _Color.rgb;

                fixed3 dithered = fixed3(
                    dither(color.r, gid),
                    dither(color.g, gid),
                    dither(color.b, gid)
                );

                // return color;
                return lerp(dithered, color, 0.5);
            }

            float2 projectNormal(float3 p, float3 n) {
                float3 x = normalize(cross(n, float3(0.8, 1.2, 7.1)));
                float3 y = normalize(cross(n, x));
                return float2(dot(p, x), dot(p, y));
            }

            float2x2 invert(float2x2 m) {
                return float2x2(m[1][1], -m[0][1], -m[1][0], m[0][0]) / determinant(m);
            }

            float2x2 jacobian(float2 f) {
                return transpose(float2x2(ddx(f), ddy(f)));
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // create uniform world-space grid (cost me all my sanity)
                float2 coords2 = projectNormal(i.coords, i.normal);
                float2x2 toVertex = jacobian(coords2);
                float2x2 toUV = jacobian(i.uv);
                float2x2 uvToVertexDiff = mul(toVertex, invert(toUV));
                float2 pxSize = float2(
                    1 / length(mul(uvToVertexDiff, float2(1, 0))),
                    1 / length(mul(uvToVertexDiff, float2(0, 1)))
                ) / DENSITY;

                fixed4 albedo = fixed4(getAlbedo(i.uv, pxSize), 1);
                // return albedo;
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                fixed diffuse = max(0, dot(i.normal, lightDir));
                fixed shadow = SHADOW_ATTENUATION(i);

                float3 viewDir = normalize(i.viewDir);
                float3 reflDir = reflect(viewDir, i.normal);
                float exponent = exp(3.0 * _Glossiness);
                float specular = pow(max(0, -dot(reflDir, lightDir)), exponent) * _Glossiness;

                float ambient = ShadeSH9(half4(i.normal, 1));
                albedo.rgb *= _LightColor0 * (diffuse + specular) * shadow + ambient;
                return albedo;
                // return fixed4(tex2D(_OcclusionMap, i.uv).r, 0, 0, 1);
                // make sure the weights sum up to 1 (divide by sum of x+y+z)
                // blend /= dot(blend,1.0);
                // read the three texture projections, for x,y,z axes
                // fixed4 cx = tex2D(_MainTex, i.coords.yz);
                // fixed4 cy = tex2D(_MainTex, i.coords.xz);
                // fixed4 cz = tex2D(_MainTex, i.coords.xy);
                // blend the textures based on weights
                // fixed4 c = cx * blend.x + cy * blend.y + cz * blend.z;
                // modulate by regular occlusion map
                // c *= tex2D(_OcclusionMap, i.uv);
                // return c;
            }
            ENDHLSL
        }

        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}