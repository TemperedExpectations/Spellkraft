Shader "Custom/Terrain Mapping" {

	Properties {
		[NoScaleOffset] _FlatTex ("Albedo", 2D) = "white" {}
		[NoScaleOffset] _FlatNorm ("Normals", 2D) = "white" {}
		[NoScaleOffset] _FlatMOHS ("MOHS", 2D) = "white" {}
		_FlatTexScale("Flat Tex Scale", Float) = 1
		_FlatBumpScale("Flat Bump Scale", Float) = 1
		_FlatSlopeThreshold("Flat Slope Threshold", Range(0, 2)) = .5
		_FlatBlendAmount("Flat Blend Amount", Range(0, 1)) = .5
		
		[NoScaleOffset] _MainTex ("Albedo", 2D) = "white" {}
		[NoScaleOffset] _MainNorm ("Normals", 2D) = "white" {}
		[NoScaleOffset] _MainMOHS ("MOHS", 2D) = "white" {}
		_MainTexScale("Main Tex Scale", Float) = 1
		_MainBumpScale("Main Bump Scale", Float) = 1
		_MainSlopeThreshold("Main Slope Threshold", Range(0, 2)) = .5
		_MainBlendAmount("Main Blend Amount", Range(0, 1)) = .5

		[NoScaleOffset] _SteepTex ("Steep Texture", 2D) = "white" {}
		[NoScaleOffset] _SteepNorm ("Steep Normal", 2D) = "bump" {}
		[NoScaleOffset] _SteepMOHS ("Steep MOHS", 2D) = "white" {}
		_SteepTexScale("Steep Tex Scale", Float) = 1
		_SteepBumpScale("Steep Bump Scale", Float) = 1
        _SteepSlopeThreshold ("Steep Slope Threshold", Range(0,2)) = .5
        _SteepBlendAmount ("Steep Blend Amount", Range(0,1)) = .5

		[NoScaleOffset] _OverTex ("Overhang Texture", 2D) = "white" {}
		[NoScaleOffset] _OverNorm ("Overhang Normal", 2D) = "bump" {}
		[NoScaleOffset] _OverMOHS ("Overhang MOHS", 2D) = "white" {}
		_OverTexScale("Overhang Tex Scale", Float) = 1
		_OverBumpScale("Overhang Bump Scale", Float) = 1


		[NoScaleOffset] _DetailTex ("Texture", 2D) = "white" {}
		[NoScaleOffset] _NormalDetailMap ("Normals", 2D) = "bump" {}

		_MapScale ("Map Scale", Float) = 1

        _RimColor ("Rim Color", Color) = (0,1,0,1)
        _RimPower ("Rim Power", Float) = .5
        _RimFac ("Rim Fac", Range(0,1)) = 1

		_BlendOffset ("Blend Offset", Range(0, 0.5)) = 0.25
		_BlendExponent ("Blend Exponent", Range(1, 8)) = 2
		_BlendHeightStrength ("Blend Height Strength", Range(0, 0.99)) = 0.5

		_BaseMetallic ("Metallic", Range(0,1)) = 0
		_BaseOcclusion ("Occlusion", Range(0,1)) = 1
		_BaseSmoothness ("Smoothness", Range(0,1)) = 0
	}

	SubShader {

		Pass {
			Tags {
				"LightMode" = "ForwardBase"
			}

			CGPROGRAM

			#pragma target 3.0

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#pragma vertex MyVertexProgram
			#pragma fragment MyFragmentProgram

			#define FORWARD_BASE_PASS

			#include "Terrain Triplanar Mapping.cginc"
			#include "Terrain Lighting.cginc"

			ENDCG
		}

		Pass {
			Tags {
				"LightMode" = "ForwardAdd"
			}

			Blend One One
			ZWrite Off

			CGPROGRAM

			#pragma target 3.0

			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog

			#pragma vertex MyVertexProgram
			#pragma fragment MyFragmentProgram

			#include "Terrain Triplanar Mapping.cginc"
			#include "Terrain Lighting.cginc"

			ENDCG
		}

		Pass {
			Tags {
				"LightMode" = "Deferred"
			}

			CGPROGRAM

			#pragma target 3.0
			#pragma exclude_renderers nomrt

			#pragma multi_compile_prepassfinal
			#pragma multi_compile_instancing

			#pragma vertex MyVertexProgram
			#pragma fragment MyFragmentProgram

			#define DEFERRED_PASS

			#include "Terrain Triplanar Mapping.cginc"
			#include "Terrain Lighting.cginc"

			ENDCG
		}

		Pass {
			Tags {
				"LightMode" = "ShadowCaster"
			}

			CGPROGRAM

			#pragma target 3.0

			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing

			#pragma vertex MyShadowVertexProgram
			#pragma fragment MyShadowFragmentProgram

			#include "Terrain Shadows.cginc"

			ENDCG
		}

		Pass {
			Tags {
				"LightMode" = "Meta"
			}

			Cull Off

			CGPROGRAM

			#pragma vertex MyLightmappingVertexProgram
			#pragma fragment MyLightmappingFragmentProgram

			#define META_PASS_NEEDS_NORMALS
			#define META_PASS_NEEDS_POSITION

			#include "Terrain Triplanar Mapping.cginc"
			#include "Terrain Lightmapping.cginc"

			ENDCG
		}
	}

	CustomEditor "TerrainShaderGUI"
}
