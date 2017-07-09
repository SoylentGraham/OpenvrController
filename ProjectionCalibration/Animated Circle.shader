Shader "New Chromantics/Animated Circle"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}

		GridSize("GridSize", Range(1,30) ) = 10
		TimeSpeed("TimeSpeed", Range(0,10.0) ) = 0.5
		TimeOffset("TimeOffset", Range(0,1) ) = 0
		ColourB("ColourB", COLOR ) = (1,1,1,1)
		RingSize("RingSize", Range(0,1) ) = 0.5
		ClipRadius("ClipRadius", Range(0.01,1) ) = 0.6
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" Queue=Transparent }
		LOD 100
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			#define USE_VIEWSPACE_UV	false

			struct appdata
			{
				float4 vertex : POSITION;
				float4 SpriteColour : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 SpriteColour : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float GridSize;

			float TimeSpeed;
			float TimeOffset;

			float4 ColourB;
			float RingSize;
			float ClipRadius;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				if (USE_VIEWSPACE_UV )
					o.uv = UnityObjectToClipPos(v.vertex);
				else
					o.uv = float4( (v.uv*2)+1, 0, 1 );
				//o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				//o.uv = v.vertex;
				o.SpriteColour = v.SpriteColour;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				int GridSizei = (int)GridSize;
				float2 uv = ((i.uv / i.uv.w) + 1.0f) / 2.0f;



				float Step = 1.0f / GridSizei;
				float2 GridUv = fmod( uv, 1.0f / GridSizei ) / Step;

				GridUv = (GridUv - 0.5f) * 2.0f;

				if ( length(GridUv) > ClipRadius )
					discard;


				float Time = 1 - fmod( (_Time.y * TimeSpeed) + TimeOffset, 1 );

				float Radius = length( GridUv ) + Time;

				float Ring = fmod( Radius, RingSize ) / RingSize;
				return ( Ring < 0.5f ) ? i.SpriteColour : ColourB;
			}
			ENDCG
		}
	}
}
