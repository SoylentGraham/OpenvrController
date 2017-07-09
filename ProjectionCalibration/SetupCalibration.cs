using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public class PlayerControllerFrame
{
	public OpenvrControllerFrame	Controller;	//	for buttons
	public Vector2?					ScreenPosition; //	uv
};


[System.Serializable]
public class UnityEvent_PlayerControllerFrames : UnityEvent <List<PlayerControllerFrame>> {}


[System.Serializable]
public class CalibrationParams
{
	public Vector3[]	WorldScreenPositions = new Vector3[4] {Vector3.zero,Vector3.zero,Vector3.zero,Vector3.zero};
};



public class SetupCalibration : MonoBehaviour {

	public string								CalibrationFilename = "Calibration.json.txt";
	public string 								CalibrationFilePath{	get{ return Application.persistentDataPath + System.IO.Path.DirectorySeparatorChar + CalibrationFilename; }}

	public MeshFilter							ScreenQuad;
	public UnityEngine.Events.UnityEvent		OnFinished;
	public UnityEvent_PlayerControllerFrames	OnPlayerUpdate;
	public Camera								ScreenCamera;

	public RectTransformUV						CalibrationTarget;
	public Canvas								CalibrationCanvas;

	public UnityEvent_String					OnCalibrationSaved;
	public UnityEvent_String					OnCalibrationFailedToSave;

	public enum CalibrationStates
	{
		CornerTopLeft = 0,
		CornerTopRight = 1,
		CornerBottomRight = 2,
		CornerBottomLeft = 3,
		Finished,
	};

	CalibrationParams	CalibrationData = new CalibrationParams ();
	CalibrationStates	CalibrationState = CalibrationStates.CornerTopLeft;
	Vector3[]			WorldScreenPositions	{	get{	return CalibrationData.WorldScreenPositions;	}}
	Vector3[]			WorldScreenUvs = new Vector3[4]{new Vector3(0,0,0),new Vector3(1,0,1),new Vector3(1,1,2),new Vector3(0,1,3)};

	[Header("Tilt controller rotation so it's more natural for the user")]
	[Range(-45,45)]
	public float		ControllerPitchTilt = 0;

	[Header("Todo: move these things to client side, not here")]
	public bool			TargetIsCanvas = false;

	void SaveCalibration()
	{
		try
		{
			var Json = JsonUtility.ToJson (CalibrationData, true);
			System.IO.File.WriteAllText( CalibrationFilePath, Json );
			var Message = "Calibration saved to " + CalibrationFilePath;
			OnCalibrationSaved.Invoke(Message);
		}
		catch(System.Exception e) {
			var Message = "Calibration failed to save to " + CalibrationFilePath + ", " + e.Message;
			OnCalibrationFailedToSave.Invoke (Message);
		}
	}

	void UpdateCalibration(Vector3 Position)
	{
		if ( CalibrationState == CalibrationStates.Finished )
			CalibrationState = CalibrationStates.CornerTopLeft;

		var StateIndex = (int)CalibrationState;
		WorldScreenPositions[StateIndex] = Position;

		Debug.Log ("Updated calibration " + CalibrationState);

		CalibrationState = (CalibrationStates)StateIndex+1;

		if (CalibrationState == CalibrationStates.Finished)
			SaveCalibration ();

		UpdateCalibrationTarget ();

		CreateScreenQuad();
	}

	public void OnControllerUpdate(List<OpenvrControllerFrame> Controllers)
	{
		foreach ( var Controller in Controllers )
		{
			if ( Controller == null )
				continue;
			if ( Controller.AppButtonPressed )
				UpdateCalibration( Controller.Position );						
		}

		var mc = ScreenQuad.GetComponent<MeshCollider>();

		//	pass player controls on
		if ( CalibrationState == CalibrationStates.Finished )
		{
			var PlayerControllers = new List<PlayerControllerFrame>();

			//	get ray from controller 
			foreach ( var Controller in Controllers )
			{
				var Player = new PlayerControllerFrame();
				Player.Controller = Controller;

				var ControllerRotation = Player.Controller.Rotation * Quaternion.AngleAxis (ControllerPitchTilt, Vector3.right);
				var Ray = new Ray( Player.Controller.Position, ControllerRotation * Vector3.forward );
				var Hit = new RaycastHit();
				if ( mc.Raycast( Ray, out Hit, 1000.0f ) )
				{
					if (TargetIsCanvas) {
						var Viewport3 = new Vector3 (Hit.textureCoord.x, Hit.textureCoord.y, -ScreenCamera.transform.position.z);
						var Screen3 = ScreenCamera.ViewportToWorldPoint (Viewport3);
						//Debug.Log (Screen3);
						//Screen3.x /= Screen3.z;
						//Screen3.y /= Screen3.z;
						Player.ScreenPosition = new Vector2 (Screen3.x, Screen3.y);
					} else {
						//	gr: flip for world, not for canvas
						var Viewport3 = new Vector3 (Hit.textureCoord.x, 1 - Hit.textureCoord.y, 0);
						var Screen3 = ScreenCamera.ViewportToWorldPoint (Viewport3);

						Player.ScreenPosition = new Vector2 (Screen3.x, Screen3.y);
					}
				}
				PlayerControllers.Add(Player);
			}
			
			OnPlayerUpdate.Invoke( PlayerControllers );
		}
	}


	void UpdateCalibrationTarget()
	{
		switch ( CalibrationState )
		{
		case CalibrationStates.CornerBottomLeft:
		case CalibrationStates.CornerBottomRight:
		case CalibrationStates.CornerTopLeft:
		case CalibrationStates.CornerTopRight:
			var Uv3 = WorldScreenUvs [(int)CalibrationState];
			CalibrationTarget.SetPositionUv (new Vector2 (Uv3.x, Uv3.y));
			CalibrationCanvas.gameObject.SetActive (true);
			CalibrationTarget.enabled = true;
			break;

		default:
			CalibrationCanvas.gameObject.SetActive (false);
			CalibrationTarget.enabled = false;
			break;
		}
	}

	void CreateScreenQuad()
	{
		var mesh = new Mesh();
		mesh.name = "World Screen Quad";

		var Vertexes = WorldScreenPositions;
		var uvs = WorldScreenUvs;

		var TriangleIndexes = new int[6];
		TriangleIndexes[0] = 0;
		TriangleIndexes[1] = 1;
		TriangleIndexes[2] = 2;

		TriangleIndexes[3] = 0;
		TriangleIndexes[4] = 2;
		TriangleIndexes[5] = 3;

		mesh.SetVertices( new List<Vector3>(Vertexes) );
		mesh.SetUVs( 0, new List<Vector3>(uvs) );
		mesh.SetIndices( TriangleIndexes, MeshTopology.Triangles, 0 );

		ScreenQuad.sharedMesh = mesh;

		var mc = ScreenQuad.GetComponent<MeshCollider>();
		if ( mc )
			mc.sharedMesh = mesh;
	}

	void OnEnable()
	{
		//	try and load calibration
		try
		{
			var CalibrationJson = System.IO.File.ReadAllText( CalibrationFilePath );
			CalibrationData = JsonUtility.FromJson<CalibrationParams>( CalibrationJson );
			CalibrationState = CalibrationStates.Finished;
		}
		catch(System.Exception e) {
			Debug.LogError ("Didn't load calibration from " + CalibrationFilePath + ", exception=" + e.Message);
		}
		
		UpdateCalibrationTarget ();
		CreateScreenQuad();
	}

}
