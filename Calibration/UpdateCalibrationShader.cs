using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateCalibrationShader : MonoBehaviour {

	public bool AlwaysVisible = true;
	ControllerTracker[] TrackObjects;
	public string TrackObjectsUniformPrefix = "ObjectPos";


	NormalisedCalibration	TheNormalisedCalibration{ get { return GameObject.FindObjectOfType<NormalisedCalibration>(); } }


	public Material CalibrationMaterial
	{
		get { return GetComponent<UnityEngine.UI.RawImage>().material; }
	}

	public static T[] FindObjectsOfTypeIncludingDisabled<T>()
	{
		var ActiveScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
		var RootObjects = ActiveScene.GetRootGameObjects();
		var MatchObjects = new List<T>();

		foreach (var ro in RootObjects)
		{
			var Matches = ro.GetComponentsInChildren<T>(true);
			MatchObjects.AddRange(Matches);
		}

		return MatchObjects.ToArray();
	}

	void OnEnable()
	{
		TrackObjects = FindObjectsOfTypeIncludingDisabled<ControllerTracker>();
	}

	enum ShaderObjectType
	{
		TYPE_NULL=		0,
		TYPE_ACTIVE=	1,
		TYPE_INACTIVE=	2,
		TYPE_EDITING=	3,
		TYPE_COUNT=		4,
	}
	const string Shader_ObjectUniformPrefix = "ObjectPos";
	const string Shader_CalibrationUniformPrefix = "CalibrationPos";


	void UpdateNormalisedCalibration()
	{
		var Calibration = TheNormalisedCalibration;
		var Mat = CalibrationMaterial;
		bool ShowCalibration = false;

		System.Action<Vector2,int,ShaderObjectType> SetObjectPos = (Pos2,Index,Type) =>
		{
			var Pos4 = new Vector4(Pos2.x, Pos2.y, (float)Type, 0);
			var Uniform = Shader_ObjectUniformPrefix + Index;
			Mat.SetVector(Uniform,Pos4);
		};

		System.Action<Vector2,int,ShaderObjectType> SetCalibrationPos = (Pos2,Index,Type) =>
		{
			var Pos4 = new Vector4(Pos2.x, Pos2.y, (float)Type, 0 );
			var Uniform = Shader_CalibrationUniformPrefix + Index;
			Mat.SetVector(Uniform,Pos4);
		};

		//	set calibration shader stuff
		{
			var CalibTypes = new ShaderObjectType[4];

			int EditingIndex = -1;

			if (Calibration.PendingCalibrationParams != null)
			{
				ShowCalibration = true;
				EditingIndex = Calibration.PendingCalibrationParams.GetEditingIndex();
			}
			else if (Calibration.CalibrationParams != null)
			{
				EditingIndex = 4;
			}
			else
			{
				ShowCalibration = true;
			}

			for (int i = 0; i < CalibTypes.Length; i++)
			{
				if (i < EditingIndex)
					CalibTypes[i] = ShaderObjectType.TYPE_ACTIVE;
				else if (i == EditingIndex)
					CalibTypes[i] = ShaderObjectType.TYPE_EDITING;
				else
					CalibTypes[i] = ShaderObjectType.TYPE_INACTIVE;
			}

			var LocalPositons = TFloorCalibration.GetEditingLocalPositions();
			for (int i = 0; i < LocalPositons.Length; i++)
			{
				SetCalibrationPos(LocalPositons[i], i, CalibTypes[i]);
			}
		}


		if (TrackObjects != null)
		{
			bool AnyActive = false;
			bool AnyGrip = false;

			for (int ObjectIndex = 0; ObjectIndex < TrackObjects.Length; ObjectIndex++)
			{
				try
				{
					var Object = TrackObjects[ObjectIndex].transform;
					var Active = Object.gameObject.activeInHierarchy;
				
					AnyGrip |= TrackObjects[ObjectIndex].GripDown;
					AnyActive |= Active;
				
					var WorldPos = Object.position;
					var FloorPos = Calibration.GetNormalisedPosition2D(WorldPos);
					var Type = Active ? ShaderObjectType.TYPE_ACTIVE : ShaderObjectType.TYPE_INACTIVE;

					SetObjectPos(FloorPos, ObjectIndex, Type);
				}
				catch(System.Exception)
				{
				}
			}

			ShowCalibration = ShowCalibration || AnyGrip || (!AnyActive);
		}

		var ri = GetComponent<UnityEngine.UI.RawImage>();
		ri.enabled = ShowCalibration || AlwaysVisible;
	}



	void Update()
	{
		UpdateNormalisedCalibration();
	}
}
