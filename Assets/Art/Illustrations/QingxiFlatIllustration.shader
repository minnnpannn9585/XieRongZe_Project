Shader "Qingxi/Flat Illustration"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Flatness ("Flatness", Range(0, 1)) = 1
        _PaletteSmoothness ("Palette Smoothness (Texels)", Range(0, 3)) = 2
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        // An untagged unlit pass follows URP's own Sprite-Unlit-Default shader.
        // It is accepted by the 2D renderer without participating in 2D lighting.
        Pass
        {
            Name "FlatIllustrationUnlit"

            HLSLPROGRAM
            #pragma vertex FlatIllustrationVertex
            #pragma fragment FlatIllustrationFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);
            float4 _MainTex_TexelSize;

            // Keep the layout identical for all variants so SRP batching stays available.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Flatness;
                half _PaletteSmoothness;
                half _EnableExternalAlpha;
            CBUFFER_END

            // Authored sRGB swatches, shared by every environment, prop and character.
            // Few, widely spaced shades collapse gentle source-image variation into
            // flat shapes. No luminance bands, spatial noise or dithering are added.
            static const float3 QingxiPalette[24] =
            {
                float3(245, 236, 206) / 255.0, // cream paper / paths
                float3(255, 252, 243) / 255.0, // warm white highlights
                float3(23, 63, 54) / 255.0,    // deep pine
                float3(44, 87, 73) / 255.0,    // forest green
                float3(85, 122, 92) / 255.0,   // mid pine
                float3(141, 159, 103) / 255.0, // sage leaves
                float3(165, 182, 129) / 255.0, // meadow green
                float3(39, 143, 166) / 255.0,  // blue water / equipment
                float3(92, 180, 186) / 255.0,  // clear turquoise water
                float3(137, 204, 203) / 255.0, // shallow water
                float3(66, 111, 119) / 255.0,  // distant blue mountains
                float3(103, 147, 151) / 255.0, // misty blue green
                float3(214, 167, 68) / 255.0,  // ochre fields
                float3(238, 198, 110) / 255.0, // straw / timber
                float3(152, 115, 68) / 255.0,  // earth / timber detail
                float3(206, 119, 72) / 255.0,  // terracotta
                float3(234, 105, 41) / 255.0,  // orange roofs / safety gear
                float3(180, 176, 160) / 255.0, // warm stone
                float3(213, 209, 198) / 255.0, // pale stone
                float3(128, 127, 115) / 255.0, // rock detail
                float3(51, 69, 73) / 255.0,    // slate roofs
                float3(233, 178, 139) / 255.0, // skin
                float3(245, 206, 170) / 255.0, // light skin
                float3(37, 49, 43) / 255.0    // ink / hair
            };

            float3 NearestPaletteColor(float3 srgb)
            {
                float3 nearest = QingxiPalette[0];
                float nearestDistance = 1e6;
                [unroll]
                for (int i = 0; i < 24; ++i)
                {
                    float3 difference = srgb - QingxiPalette[i];
                    float distance = dot(difference, difference);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = QingxiPalette[i];
                    }
                }
                return nearest;
            }

            half4 SampleIllustration(float2 uv)
            {
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                #if defined(ETC1_EXTERNAL_ALPHA)
                    half externalAlpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
                    sampled.a = lerp(sampled.a, externalAlpha, _EnableExternalAlpha);
                #endif
                return sampled;
            }

            float3 SmoothPaletteSource(float2 uv, half4 center)
            {
                if (_PaletteSmoothness <= 0.001) return center.rgb;

                float2 stepUV = abs(_MainTex_TexelSize.xy) * _PaletteSmoothness;
                float3 totalColor = 0;
                float totalWeight = 0;
                [unroll]
                for (int y = -1; y <= 1; ++y)
                {
                    [unroll]
                    for (int x = -1; x <= 1; ++x)
                    {
                        half4 neighbor = (x == 0 && y == 0)
                            ? center : SampleIllustration(uv + float2(x, y) * stepUV);
                        float spatialWeight = (x == 0 ? 2.0 : 1.0) * (y == 0 ? 2.0 : 1.0);
                        // Ignore hidden RGB and strongly downweight nearly transparent fringes.
                        float alphaWeight = neighbor.a * neighbor.a;
                        // Keep distinct edges such as windows and roofs from bleeding together.
                        float3 difference = neighbor.rgb - center.rgb;
                        float edgeWeight = saturate(1.0 - dot(difference, difference) * 16.0);
                        float weight = spatialWeight * alphaWeight * edgeWeight * edgeWeight;
                        totalColor += neighbor.rgb * weight;
                        totalWeight += weight;
                    }
                }
                return totalWeight > 0.00001 ? totalColor / totalWeight : center.rgb;
            }

            Varyings FlatIllustrationVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                // Use URP's sprite instance tint exactly once, as its built-in shader does.
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 FlatIllustrationFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Sprite UVs already include the atlas rect; do not apply a material ST.
                half4 source = SampleIllustration(input.uv);
                // Flatness zero is an exact original-texture path, with no filter work.
                if (_Flatness <= 0.0 || source.a <= 0.0) return source * input.color;

                // sRGB texture sampling is decoded by the GPU in Linear projects.
                // Compare in the palette's sRGB space, then return to the render space.
                // Only palette selection is smoothed. Silhouettes retain their original alpha.
                float3 srgb = SmoothPaletteSource(input.uv, source);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    srgb = LinearToSRGB(srgb);
                #endif
                float3 flatColor = NearestPaletteColor(srgb);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    flatColor = SRGBToLinear(flatColor);
                #endif

                source.rgb = lerp(source.rgb, flatColor, saturate(_Flatness));
                // Apply tints after mapping: fades remain smooth and source alpha is untouched.
                return source * input.color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
