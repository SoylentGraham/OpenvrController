using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public class TFloorCalibration
{
	//	world space
	public Vector3 FrontLeft;
	public Vector3 FrontRight;
	public Vector3 BackLeft;
	public Vector3 BackRight;

	static public Vector2 Local_FrontLeft { get { return new Vector2(0, 0); } }
	static public Vector2 Local_FrontRight { get { return new Vector2(1, 0); } }
	static public Vector2 Local_BackLeft { get { return new Vector2(0, 1); } }
	static public Vector2 Local_BackRight { get { return new Vector2(1, 1); } }

	//	match GetEditingIndex()
	static public Vector2[] GetEditingLocalPositions()
	{
		return new Vector2[4] { Local_FrontLeft, Local_FrontRight, Local_BackRight, Local_BackLeft };
	}

	float GetMidHeight()
	{
		return (FrontLeft.y+FrontRight.y+BackLeft.y+BackRight.y) / 4.0f;
	}

	float GetHeightRange(float HeightScalar)
	{
		var SceneWidth = Vector2.Distance(FrontLeft.xz(), FrontRight.xz());
		return SceneWidth * HeightScalar;
	}

	float GetLocalHeight(float WorldHeight,float HeightScalar)
	{
		var HalfHeight = GetHeightRange(HeightScalar) / 2.0f;
		var Mid = GetMidHeight();
		var Top = Mid - HalfHeight;
		var Bottom = Mid + HalfHeight;
		return PopMath.Range(Bottom, Top, WorldHeight);
	}

	public Matrix4x4 GetWorldToLocalTransform()
	{
		var Src = new Vector2[4] { FrontLeft.xz(), FrontRight.xz(), BackLeft.xz(), BackRight.xz() };
		var Dst = new Vector2[4] { Local_FrontLeft, Local_FrontRight, Local_BackLeft, Local_BackRight };
		return PopX.Homography.CalcHomography(Src, Dst);
	}

	public Vector3 GetWorldToLocal(Vector3 Position,float HeightScalar)
	{
		var Mtx = GetWorldToLocalTransform();
		var Worldy = Position.y;
		Position = Position.xzy();
		Position = Mtx.MultiplyPoint(Position);
		Position = Position.xzy();
		Position.y = GetLocalHeight(Worldy,HeightScalar);
		return Position;
	}
}

public class TPendingFloorCalibration
{
	//	world space
	public Vector3? FrontLeft;
	public Vector3? FrontRight;
	public Vector3? BackLeft;
	public Vector3? BackRight;

	public int		GetEditingIndex()
	{
		if (!FrontLeft.HasValue) return 0;
		if (!FrontRight.HasValue) return 1;
		if (!BackRight.HasValue) return 2;
		if (!BackLeft.HasValue) return 3;
		return 4;
	}

	public void		SetNextEditingPosition(Vector3 Position)
	{
		if (!FrontLeft.HasValue) FrontLeft = Position;
		else if (!FrontRight.HasValue) FrontRight = Position;
		else if (!BackRight.HasValue) BackRight = Position;
		else if (!BackLeft.HasValue) BackLeft = Position;
		else
			throw new System.Exception("There is no current editing position");
	}

	public bool		IsFinished()
	{
		return GetEditingIndex() == 4;
	}

	public TFloorCalibration GetCalibrationParameters()
	{
		var Params = new TFloorCalibration();
		Params.FrontLeft = FrontLeft.HasValue ? FrontLeft.Value : Vector3.zero;
		Params.FrontRight = FrontRight.HasValue ? FrontRight.Value : Vector3.zero;
		Params.BackLeft = BackLeft.HasValue ? BackLeft.Value : Vector3.zero;
		Params.BackRight = BackRight.HasValue ? BackRight.Value : Vector3.zero;
		return Params;
	}
}




public class NormalisedCalibration : MonoBehaviour {

	[Range(0, 2)]
	public float HeightScalar = 1;

	public bool QuitOnAnyKey = true;
	public List<KeyCode>	QuitAppKeys;

	class AppButtonHold
	{
		public AppButtonHold(int ControllerIndex)
		{
			HoldStartTime = Time.time;
			Triggered = false;
			this.ControllerIndex = ControllerIndex;
		}

		public int			ControllerIndex;
		float				HoldStartTime;
		public float		HoldDuration {	get { return Time.time - HoldStartTime;} }
		public bool			Triggered;
	}

	[Range(0,10)]
	public float					HoldToRecalibrateDuration = 2;
	AppButtonHold					CurrentHold = null;

	public bool						NextSceneIfShortAppButtonPress = true;

	public bool						IsCalibrated	{ get { return CalibrationParams != null; }}
	public bool						IsCalibrating	{ get { return PendingCalibrationParams != null; }}
	public UnityEvent				OnCalibrationChanged;
	public bool						IsCalibratingJustFinished = false;

	const string CalibrationSaveKey = "Calibration";

	[System.NonSerialized]
	public TFloorCalibration		CalibrationParams;
	[System.NonSerialized]
	public TPendingFloorCalibration	PendingCalibrationParams;

	public enum CalibrationPlane
	{
		Floor,
		Wall,
	};
	public CalibrationPlane Plane = CalibrationPlane.Floor;

	public Vector2 GetFloorPosition(Vector3 WorldPosition)
	{
		return GetNormalisedPosition(WorldPosition).xz();
	}

	public Vector2 GetWallPosition(Vector3 WorldPosition)
	{
		return GetNormalisedPosition(WorldPosition).xy();
	}

	public Vector2 GetNormalisedPosition2D(Vector3 WorldPosition)
	{
		if (Plane == CalibrationPlane.Floor)
			return GetFloorPosition(WorldPosition);
		else if (Plane == CalibrationPlane.Wall)
			return GetWallPosition(WorldPosition);
		else
			throw new System.Exception("Unknown plane " + Plane);
	}


	public Vector3 GetNormalisedPosition(Vector3 WorldPosition)
	{
		try
		{
			var LocalPos = CalibrationParams.GetWorldToLocal(WorldPosition,HeightScalar);
			return LocalPos;
		}
		catch (System.Exception e)
		{
			throw new System.Exception("Calibration not configured: " + e.Message);
		}
	}


	void OnCancelledCalibrationHold()
	{
		if ( NextSceneIfShortAppButtonPress )
		{
			GotoNextScene.LoadNextScene();
		}
	}

	void ResetCalibration()
	{
		PendingCalibrationParams = new TPendingFloorCalibration();
		OnCalibrationChanged.Invoke();
	}

	void OnCalibrationClick(Vector3 WorldPos)
	{
		//	gr: now wait for other thing to START calibration, so if no pending, not started
		//	start pending if we weren't
		if ( PendingCalibrationParams == null )
		{
			//ResetCalibration();
			return;
		}
		
		//	if pending, update pending
		PendingCalibrationParams.SetNextEditingPosition(WorldPos);
		if ( PendingCalibrationParams.IsFinished() )
		{
			CalibrationParams = PendingCalibrationParams.GetCalibrationParameters();
			PendingCalibrationParams = null;
			SaveCalibration();
		}
		OnCalibrationChanged.Invoke();

		IsCalibratingJustFinished = true;
	}

	void UpdateCalibrationHold(bool Down,int ControllerIndex)
	{
		//	is there a hold related to this controller?
		if ( CurrentHold == null && !Down )
			return;
		if ( CurrentHold !=null && CurrentHold.ControllerIndex != ControllerIndex )
			return;

		//	release
		if ( !Down )
		{
			var DoCancel = !CurrentHold.Triggered;
			CurrentHold = null;

			if ( IsCalibrating )
				DoCancel = false;

			if ( IsCalibratingJustFinished )
				DoCancel = false;

			if ( DoCancel )
				OnCancelledCalibrationHold();

			IsCalibratingJustFinished = false;

			return;
		}

		if ( CurrentHold == null )
			CurrentHold = new AppButtonHold(ControllerIndex);

		if ( CurrentHold.Triggered )
			return;
		
		if ( CurrentHold.HoldDuration > HoldToRecalibrateDuration)
		{
			CurrentHold.Triggered = true;
			ResetCalibration();
			return;
		}
	}

	void OnControllersUpdate(List<OpenvrControllerFrame> Controllers)
	{
		//	check for calibration invocation
		for ( var i=0;	i<Controllers.Count;	i++)
		{
			try
			{
				var Controller = Controllers[i];
				if (Controller == null ||!Controller.Attached || !Controller.Tracking)
				{
					//	cancel stuff if gone missing
					UpdateCalibrationHold(false,i);
					continue;
				}

				UpdateCalibrationHold(Controller.AppButtonIsDown,i);
			
				if (Controller.AppButtonPressed)
					OnCalibrationClick(Controller.Position);
		
			}
			catch (System.Exception e)
			{
				Debug.LogException(e);
			}
		}
	}

	void OnEnable()
	{
		var Controllers = GameObject.FindObjectOfType<OpenvrControllerManager>();
		if (Controllers != null)
			Controllers.OnUpdateAll.AddListener(OnControllersUpdate);

		//	try and load calibration
		LoadCalibration();
	}

	void OnDisable()
	{
		var Controllers = GameObject.FindObjectOfType<OpenvrControllerManager>();
		if (Controllers != null)
			Controllers.OnUpdateAll.RemoveListener(OnControllersUpdate);
	}


	void SaveCalibration()
	{
		var Json = JsonUtility.ToJson(CalibrationParams);
		PlayerPrefs.SetString(CalibrationSaveKey, Json);
		PlayerPrefs.Save();
	}


	void LoadCalibration()
	{
		var Json = PlayerPrefs.GetString(CalibrationSaveKey);
		var Calibration = JsonUtility.FromJson<TFloorCalibration>(Json);
		CalibrationParams = Calibration;
		OnCalibrationChanged.Invoke();
	}

	void Update()
	{
		if ( QuitOnAnyKey && Input.anyKeyDown )
			Application.Quit();

		Pop.AllocIfNull(ref QuitAppKeys);
		foreach (var Key in QuitAppKeys)
		{ 
			if ( Input.GetKey(Key) )
				Application.Quit();
		}
	}
}
