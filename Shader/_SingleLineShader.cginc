#ifndef VOL_LINE_SINGLE_LINE_SHADER_INC
#define VOL_LINE_SINGLE_LINE_SHADER_INC
	
	#include "UnityCG.cginc"
	
	// Property-variables declarations
	sampler2D _MainTex;
	float _LineWidth;
	float _LineScale;
	fixed4 _Color;
	float _Intensity;
#ifdef LIGHT_SABER_MODE_ON
	fixed _LightSaberFactor;
	int _UvBasedLightSaberFactor;
#endif

	// Vertex shader input attributes
	struct a2v
	{
		float4 vertex : POSITION;
		float3 otherPos : NORMAL; // object-space position of the other end
		half2 texcoord : TEXCOORD0;
		float2 texcoord1 : TEXCOORD1;

		UNITY_VERTEX_INPUT_INSTANCE_ID
	};
	
	// Vertex out/fragment in data:
	struct v2f
	{
		float4 pos : SV_POSITION;
		half2 uv : TEXCOORD0;

		UNITY_VERTEX_INPUT_INSTANCE_ID
		UNITY_VERTEX_OUTPUT_STEREO
	};
	
	// Vertex shader
	v2f vert (a2v v)
	{
		v2f o;

		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_INITIALIZE_OUTPUT(v2f, o);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

		// Pass on texture coordinates to fragment shader as they are:
		o.uv = v.texcoord;
		
		// Transform to homogeneous clip space:
		float4 csPos = UnityObjectToClipPos(v.vertex);
		float4 csPos_other = UnityObjectToClipPos(float4(v.otherPos, 1.0));

		// Scale to properly match Unity's world space units:
		// The `projScale` factor also handles different field of view values, which 
		// used to be handled via FOV_SCALING_OFF in previous versions of this asset.
		// Furthermore, `projScale` handles orthographic projection matrices gracefully.
		float projScale = unity_CameraProjection._m11 * 0.5;
		float scaledLineWidth = _LineWidth * _LineScale * projScale;

		float aspectRatio = unity_CameraProjection._m11 / unity_CameraProjection._m00;
		// The line direction in (aspect-ratio corrected) clip space (and scaled by witdh):
		float2 lineDirProj = normalize(
			csPos.xy * aspectRatio / csPos.w - // screen-space pos of current end
			csPos_other.xy * aspectRatio / csPos_other.w // screen-space position of the other end
		) * sign(csPos.w) * sign(csPos_other.w) * scaledLineWidth;
		
		// Offset for our current vertex:
		float2 offset =
			v.texcoord1.x * lineDirProj +
			v.texcoord1.y * float2(lineDirProj.y, -lineDirProj.x)
		;

		// Apply (aspect-ratio corrected) offset
		csPos.x += offset.x / aspectRatio;
		csPos.y += offset.y;
		o.pos = csPos;

		return o;
	}
	
	// Fragment shader
	fixed4 frag(v2f i) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
		fixed4 tx = tex2D(_MainTex, i.uv);

#ifdef LIGHT_SABER_MODE_ON
		if (_UvBasedLightSaberFactor == 1) 
		{
			float2 uv2 = i.uv * 2.0 - 1.0;
			float c = sqrt(uv2[0] * uv2[0] + uv2[1] * uv2[1]);
			tx = lerp(tx * _Color, float4(1.0, 1.0, 1.0, tx.a), clamp((1.02 - c - _LightSaberFactor) * 100.0, 0, 1));
		}
		else 
		{
			tx = tx.a > _LightSaberFactor ? float4(1.0, 1.0, 1.0, tx.a) : tx * _Color;
		}
		return tx * (_Intensity * _Color.a);
#else
        return (tx * _Color) * _Intensity;
#endif
	}
	
#endif
