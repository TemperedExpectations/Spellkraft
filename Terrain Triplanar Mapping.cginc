#if !defined(MY_TRIPLANAR_MAPPING_INCLUDED)
#define MY_TRIPLANAR_MAPPING_INCLUDED

#define NO_DEFAULT_UV

#include "Terrain Lighting Input.cginc"

half _FlatTexScale, _FlatBumpScale, _FlatSlopeThreshold, _FlatBlendAmount;
half _MainTexScale, _MainBumpScale,_MainSlopeThreshold, _MainBlendAmount;
half _SteepTexScale, _SteepBumpScale,_SteepSlopeThreshold, _SteepBlendAmount;
half _OverTexScale, _OverBumpScale;
half _RimPower, _RimFac;

fixed4 _RimColor;

sampler2D _FlatTex, _FlatNorm, _FlatMOHS;
sampler2D _MainMOHS, _NormalDetailMap;
sampler2D _SteepTex, _SteepNorm, _SteepMOHS;
sampler2D _OverTex, _OverNorm, _OverMOHS;

float _MapScale;

float _BlendOffset, _BlendExponent, _BlendHeightStrength;

float _BaseMetallic, _BaseOcclusion, _BaseSmoothness;

struct TriplanarUV {
	float2 x, y, z;
};

struct Triplanar {
	float4 x, y, z;
};

TriplanarUV GetTriplanarUV (SurfaceParameters parameters) {
	TriplanarUV triUV;
	float3 p = parameters.position / _MapScale;
	triUV.x = p.zy;
	triUV.y = p.xz;
	triUV.z = p.xy;
	if (parameters.normal.x < 0) {
		triUV.x.x = -triUV.x.x;
	}
	if (parameters.normal.y < 0) {
		triUV.y.x = -triUV.y.x;
	}
	if (parameters.normal.z >= 0) {
		triUV.z.x = -triUV.z.x;
	}
	triUV.x.y += 0.5;
	triUV.z.x += 0.5;
	return triUV;
}

float3 GetTriplanarWeights (SurfaceParameters parameters, float heightX, float heightY, float heightZ) {
	float3 triW = abs(parameters.normal);
	triW = saturate(triW - _BlendOffset);
	triW *= lerp(1, float3(heightX, heightY, heightZ), _BlendHeightStrength);
	triW = pow(triW, _BlendExponent);
	return triW / (triW.x + triW.y + triW.z);
}

float3 BlendTriplanarNormal (float3 mappedNormal, float3 surfaceNormal) {
	float3 n;
	n.xy = mappedNormal.xy + surfaceNormal.xy;
	n.z = mappedNormal.z * surfaceNormal.z;
	return n;
}

Triplanar GetTriplanar(sampler2D tex, TriplanarUV worldUVs, float scale, float weight) {
	Triplanar mapping;
	mapping.x = tex2D(tex, worldUVs.x / scale) * weight;
	mapping.y = tex2D(tex, worldUVs.y / scale) * weight;
	mapping.z = tex2D(tex, worldUVs.z / scale) * weight;
	return mapping;
}

void MyTriPlanarSurfaceFunction (inout SurfaceData surface, SurfaceParameters parameters) {
	TriplanarUV triUV = GetTriplanarUV(parameters);
	float slope = 1 - parameters.normal.y;
	
    float flatBlendHeight = _FlatSlopeThreshold * (1-_FlatBlendAmount);
    float flatWeight = 1-saturate((slope-flatBlendHeight)/(_FlatSlopeThreshold-flatBlendHeight));

	Triplanar albedoFlat = GetTriplanar(_FlatTex, triUV, _FlatTexScale, flatWeight);
	Triplanar rawNormalFlat = GetTriplanar(_FlatNorm, triUV, _FlatTexScale, flatWeight);
	Triplanar mohsFlat = GetTriplanar(_FlatMOHS, triUV, _FlatTexScale, flatWeight);
	
	
    float mainBlendHeight = (_MainSlopeThreshold - _FlatSlopeThreshold) * (1-_MainBlendAmount) + _FlatSlopeThreshold;
    float mainWeight = 1-saturate((slope-mainBlendHeight)/(_MainSlopeThreshold-mainBlendHeight));

	Triplanar albedoMain = GetTriplanar(_MainTex, triUV, _MainTexScale, mainWeight - flatWeight);
	Triplanar rawNormalMain = GetTriplanar(_MainNorm, triUV, _MainTexScale, mainWeight - flatWeight);
	Triplanar mohsMain = GetTriplanar(_MainMOHS, triUV, _MainTexScale, mainWeight - flatWeight);
	
	
    float steepBlendHeight = (_SteepSlopeThreshold - _MainSlopeThreshold - _FlatSlopeThreshold) * (1-_SteepBlendAmount) + _MainSlopeThreshold + _FlatSlopeThreshold;
    float steepWeight = 1-saturate((slope-steepBlendHeight)/(_SteepSlopeThreshold-steepBlendHeight));

	Triplanar albedoSteep = GetTriplanar(_SteepTex, triUV, _SteepTexScale, steepWeight - mainWeight);
	Triplanar rawNormalSteep = GetTriplanar(_SteepNorm, triUV, _SteepTexScale, steepWeight - mainWeight);
	Triplanar mohsSteep = GetTriplanar(_SteepMOHS, triUV, _SteepTexScale, steepWeight - mainWeight);

	Triplanar albedoOver = GetTriplanar(_OverTex, triUV, _OverTexScale, 1 - steepWeight);
	Triplanar rawNormalOver = GetTriplanar(_OverNorm, triUV, _OverTexScale, 1 - steepWeight);
	Triplanar mohsOver = GetTriplanar(_OverMOHS, triUV, _OverTexScale, 1 - steepWeight);

	albedoMain.x += albedoFlat.x + albedoSteep.x + albedoOver.x;
	albedoMain.y += albedoFlat.y + albedoSteep.y + albedoOver.y;
	albedoMain.z += albedoFlat.z + albedoSteep.z + albedoOver.z;

	rawNormalMain.x += rawNormalFlat.x + rawNormalSteep.x + rawNormalOver.x;
	rawNormalMain.y += rawNormalFlat.y + rawNormalSteep.y + rawNormalOver.y;
	rawNormalMain.z += rawNormalFlat.z + rawNormalSteep.z + rawNormalOver.z;

	mohsMain.x += mohsFlat.x + mohsSteep.x + mohsOver.x;
	mohsMain.y += mohsFlat.y + mohsSteep.y + mohsOver.y;
	mohsMain.z += mohsFlat.z + mohsSteep.z + mohsOver.z;

	float bumpScale = _FlatBumpScale * flatWeight + _MainBumpScale * (mainWeight - flatWeight) + _SteepBumpScale * (steepWeight - mainWeight) + _OverBumpScale * (1 - steepWeight);
	float3 tangentNormalX = UnpackScaleNormal(rawNormalMain.x, bumpScale);
	float3 tangentNormalY = UnpackScaleNormal(rawNormalMain.y, bumpScale);
	float3 tangentNormalZ = UnpackScaleNormal(rawNormalMain.z, bumpScale);

	if (parameters.normal.x < 0) {
		tangentNormalX.x = -tangentNormalX.x;
	}
	if (parameters.normal.y < 0) {
		tangentNormalY.x = -tangentNormalY.x;
	}
	if (parameters.normal.z >= 0) {
		tangentNormalZ.x = -tangentNormalZ.x;
	}

	float3 worldNormalX = BlendTriplanarNormal(tangentNormalX, parameters.normal.zyx).zyx;
	float3 worldNormalY = BlendTriplanarNormal(tangentNormalY, parameters.normal.xzy).xzy;
	float3 worldNormalZ = BlendTriplanarNormal(tangentNormalZ, parameters.normal);

	float3 triW = GetTriplanarWeights(parameters, mohsMain.x.z, mohsMain.y.z, mohsMain.z.z);

	surface.albedo = albedoMain.x * triW.x + albedoMain.y * triW.y + albedoMain.z * triW.z;

	float4 mohs = mohsMain.x * triW.x + mohsMain.y * triW.y + mohsMain.z * triW.z;

	surface.metallic = _BaseMetallic;
	surface.occlusion = _BaseOcclusion;
	surface.smoothness = _BaseSmoothness;

	surface.normal = normalize(worldNormalX * triW.x + worldNormalY * triW.y + worldNormalZ * triW.z);

	float3 worldViewDir = normalize(UnityWorldSpaceViewDir(parameters.position));
    half rim = 1.0 - saturate(dot (worldViewDir, surface.normal));
    float rimWeight =  pow (rim, _RimPower) * _RimFac;

    surface.albedo = _RimColor * rimWeight + surface.albedo * (1-rimWeight);
}

#define SURFACE_FUNCTION MyTriPlanarSurfaceFunction

#endif