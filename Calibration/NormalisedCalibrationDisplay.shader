Shader "Vera/NormalisedCalibrationDisplay"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		BackgroundColour ("BackgroundColour", COLOR) = (0,0,0,0)
		ObjectSize ("ObjectSize", Range(0.0001,0.1) ) = 0.1
		EdgeSize ("EdgeSize", Range(0.0001,0.1) ) = 0.1
		BorderSize ("BorderSize", Range(0.0001,0.1) ) = 0.1
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		BlendOp Add
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 Colour : COLOR;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 Colour : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			//float2 BoundsMin;
			//float2 BoundsMax;
			#define BoundsMin				float2(0,0)
			#define BoundsMax				float2(1,1)

			#define FlashRateSecs			1

			#define Colour_Null					float4(0,0,0,0)
			#define Colour_Active				float4(1,1,1,1)
			#define Colour_Inactive				float4(1,0,0,GetFlashAlpha())
			#define Colour_Corner_Valid			float4(0,1,0,1)
			#define Colour_Corner_Calibrating	float4(1,1,0, GetFlashAlpha() )
			#define Colour_Corner_Invalid		float4(1,0,0,1)

			#define TYPE_NULL				0
			#define TYPE_ACTIVE				1
			#define TYPE_INACTIVE			2
			#define TYPE_EDITING			3
			#define TYPE_COUNT				4


			//	z = Type
			float4 ObjectPos0;
			float4 ObjectPos1;
			float4 ObjectPos2;
			float4 ObjectPos3;
			float4 ObjectPos4;
			float4 CalibrationPos0;
			float4 CalibrationPos1;
			float4 CalibrationPos2;
			float4 CalibrationPos3;

			float4 BackgroundColour;
			float ObjectSize;
			float BorderSize;
			float EdgeSize;

			float GetFlashAlpha()
			{
				float Norm = fmod( _Time.z, FlashRateSecs ) / FlashRateSecs;
				return Norm < 0.5f ? 1 : 0;
			}


			float4 GetObjectColour(float4 Object)
			{
				float4 Colours[TYPE_COUNT];
				Colours[TYPE_NULL] = Colour_Null;
				Colours[TYPE_ACTIVE] = Colour_Active;
				Colours[TYPE_INACTIVE] = Colour_Inactive;
				Colours[TYPE_EDITING] = Colours[TYPE_INACTIVE];
			
				int Type = Object.z;
				return Colours[Type];
			}

			float4 GetCalibrationColour(float4 Calibration)
			{
				float4 Colours[TYPE_COUNT];
				Colours[TYPE_NULL] = Colour_Null;
				Colours[TYPE_ACTIVE] = Colour_Corner_Valid;
				Colours[TYPE_INACTIVE] = Colour_Corner_Invalid;
				Colours[TYPE_EDITING] = Colour_Corner_Calibrating;
			
				int Type = Calibration.z;
				return Colours[Type];
			}


			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);

				//#if !UNITY_UV_STARTS_AT_TOP
					o.uv.y = 1-o.uv.y;
				//#endif

				o.Colour = v.Colour;
				return o;
			}

			float2 GetBoundsSize()
			{
				float Width = BoundsMax.x - BoundsMin.x;
				float Height = BoundsMax.y - BoundsMin.y;
				return float2( Width, Height );
			}
			
			float GetBoundsHeightRatio()
			{
				return GetBoundsSize().y / GetBoundsSize().x;				
			}

			float Range(float Min,float Max,float Value)
			{
				return (Value-Min) / (Max-Min);
			}


			float2 UvToBoundsUv(float2 uv)
			{
				float Width = 1;
				float Height = Width * GetBoundsHeightRatio();

				float2 Border = float2( 1 - Width, 1 - Height ) / 2;

				float Left = Border.x;
				float Right = 1 - Border.x;
				float Top = Border.y;
				float Bottom = 1 - Border.y;

				uv.x = Range( Left, Right, uv.x );
				uv.y = Range( Top, Bottom, uv.y );
				return uv;
			}

			float2 UvToBoundsNorm(float2 uv)
			{
				float2 BoundsNormMin = BoundsMin;
				float2 BoundsNormMax = BoundsMax;

				uv = UvToBoundsUv(uv);

				return lerp( BoundsNormMin, BoundsNormMax, uv );
			}
			
			
			bool PointIsObject(float2 uv,float2 ObjectPos)
			{
				float2 ObjectNorm = ObjectPos;

				float2 UvNorm = UvToBoundsNorm( uv );
				float2 Distance2 = ( UvNorm - ObjectNorm );
				Distance2.y *= GetBoundsHeightRatio();
				float Distance = length( Distance2 );
				return (Distance < ObjectSize);
			}


			bool PointIsCalibration(float2 uv,float2 CalibrationPos)
			{
				float2 ObjectNorm = CalibrationPos;

				float2 UvNorm = UvToBoundsNorm( uv );
				float2 Distance2 = ( UvNorm - ObjectNorm );
				Distance2.y *= GetBoundsHeightRatio();
				float Distance = length( Distance2 );
				return (Distance < ObjectSize);
			}

		
			bool InsideMinMax(float2 uv,float2 Min,float2 Max)
			{
				return (uv.x>=Min.x) && (uv.y>=Min.x) && (uv.x<=Max.x) && (uv.y<=Max.y);
			}

			bool Inside01(float2 uv)
			{
				return InsideMinMax( uv, float2(0,0), float2(1,1) );
			}


			float4 UpdateObjectColour(float2 uv,float4 Colour,float4 ObjectPos)
			{			
				if ( PointIsObject( uv, ObjectPos.xy ) )
				{
					float4 ObjectColour = GetObjectColour(ObjectPos);
					Colour = lerp( Colour, ObjectColour, ObjectColour.w );
				}
				return Colour;
			}

			float4 UpdateCalibrationColour(float2 uv,float4 Colour,float4 ObjectPos)
			{			
				if ( PointIsCalibration( uv, ObjectPos.xy ) )
				{
					float4 ObjectColour = GetCalibrationColour(ObjectPos);
					Colour = lerp( Colour, ObjectColour, ObjectColour.w );
				}
				return Colour;
			}


			float2 GetScreenRatio()
			{
				float ScreenRatio = _ScreenParams.y/_ScreenParams.x;
				if ( ScreenRatio > 1 )
					return float2( 1, ScreenRatio );
				else
					return float2( 1/ScreenRatio, 1 );
			}

			bool IsBorder(float2 uv)
			{
				//	outside
				if ( !Inside01( uv ) )
					return false;

				if ( !InsideMinMax( uv, float2(EdgeSize,EdgeSize), float2(1-EdgeSize,1-EdgeSize) ) )
				{
					return true;
				}

				//	inside

				//	show top-left marker
				float TopLeftCornerSize = EdgeSize * 1.5f;
				if ( uv.x < TopLeftCornerSize && uv.y < TopLeftCornerSize )
				{
					return true;
					//return true;
				}

				return false;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float4 DiscardColor = BackgroundColour;


				float2 uv = i.uv;
				uv -= 0.5f;
				uv *= GetScreenRatio();
				uv += 0.5f;
				uv.x = Range( BorderSize, 1-BorderSize, uv.x );
				uv.y = Range( BorderSize, 1-BorderSize, uv.y );
				

				float2 BoundsNorm = UvToBoundsNorm( uv );
				fixed4 Colour = DiscardColor;

				if ( IsBorder(BoundsNorm) )
				{
					Colour = tex2D(_MainTex, BoundsNorm) * i.Colour;
				}


				Colour = UpdateObjectColour( uv, Colour, ObjectPos0 );
				Colour = UpdateObjectColour( uv, Colour, ObjectPos1 );
				Colour = UpdateObjectColour( uv, Colour, ObjectPos2 );
				Colour = UpdateObjectColour( uv, Colour, ObjectPos3 );
				Colour = UpdateObjectColour( uv, Colour, ObjectPos4 );

				Colour = UpdateCalibrationColour( uv, Colour, CalibrationPos0 );
				Colour = UpdateCalibrationColour( uv, Colour, CalibrationPos1 );
				Colour = UpdateCalibrationColour( uv, Colour, CalibrationPos2 );
				Colour = UpdateCalibrationColour( uv, Colour, CalibrationPos3 );

				return Colour;
			}
			ENDCG
		}
	}
}
