/// Render a single volumetric line using an additive shader which does not support changing the color
/// 
/// Based on the Volumetric lines algorithm by Sebastien Hillaire
/// https://web.archive.org/web/20111202022753/http://sebastien.hillaire.free.fr/index.php?option=com_content&view=article&id=57&Itemid=74
/// Thread in the Unity3D Forum:
/// http://forum.unity3d.com/threads/181618-Volumetric-lines
/// 
/// Unity3D port by Johannes Unterguggenberger
/// johannes.unterguggenberger@gmail.com
/// 
/// Thanks to Michael Probst for support during development.
/// 
/// Thanks for bugfixes and improvements to Unity Forum User "Mistale"
/// http://forum.unity3d.com/members/102350-Mistale

/// Shader code optimization and cleanup by Lex Darlog (aka DRL)
/// http://forum.unity3d.com/members/lex-drl.67487/
/// 
/// Single Pass Stereo Support by Unity Forum User "Abnormalia_"
/// https://forum.unity.com/members/abnormalia_.356336/ 
///
/// Single pass instanced rendering fix by Niel 
/// 
Shader "Lines/SingleLine-TextureAdditive" {
	Properties {
		[NoScaleOffset] _MainTex ("Base (RGB)", 2D) = "white" {}
		_LineWidth ("Line Width", Range(0.001, 100)) = 1.0
		_LineScale ("Line Scale", Float) = 1.0
		_Color ("Main Color", Color) = (1,1,1,1)
		_Intensity("Intensity",Range(0.0,2.0)) = 1.0
	}
	SubShader {
		// batching is forcefully disabled here because the shader simply won't work with it:
		Tags {
			"DisableBatching"="True"
			"RenderType"="Transparent"
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"ForceNoShadowCasting"="True"
			"PreviewType"="Plane"
		}
		LOD 200
		
		Pass {
			
			Cull Off
			ZWrite Off
			ZTest LEqual
			Blend One One
			Lighting Off
			
			CGPROGRAM
				#pragma glsl_no_auto_normalization
				#pragma vertex vert
				#pragma fragment frag
				
			#include "UnityCG.cginc"
	
			// Property-variables declarations
			sampler2D _MainTex;
			float _LineWidth;
			float _LineScale;
			fixed4 _Color;
			float _Intensity;

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
				fixed4 tx = tex2D(_MainTex, i.uv)*_Intensity;
				return tx * _Color;
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}