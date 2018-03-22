using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR;
using UnityEngine.Events;

using System.Runtime.InteropServices;	//	marshalling
using Valve.VR;							//	openvr


[System.Serializable]
public class OpenvrControllerFrame
{
	public bool			Attached;
	public bool			Tracking;

	public Vector3		Position;
	public Quaternion	Rotation;

	public bool			TriggerIsDown;
	public bool			TriggerPressed;
	public bool			TriggerReleased;
	public float		TriggerAxis;

	public Vector2		TouchpadAxis;

	public bool			TouchpadIsDown;
	public bool			TouchpadPressed;
	public bool			TouchpadReleased;

	public bool			TouchpadClickIsDown;
	public bool			TouchpadClickPressed;
	public bool			TouchpadClickReleased;

	public bool			AppButtonIsDown;
	public bool			AppButtonPressed;
	public bool			AppButtonReleased;

	public bool			GripButtonIsDown;
	public bool			GripButtonPressed;
	public bool			GripButtonReleased;

	public bool IsKeyFrame()
	{
		return TriggerPressed || TriggerReleased ||
			TouchpadPressed || TouchpadReleased ||
			TouchpadClickPressed || TouchpadClickReleased ||
			AppButtonPressed || AppButtonReleased ||
			GripButtonPressed || GripButtonReleased;
	}

	//	calculate the Pressed/Released values
	public void CalculateDiff(OpenvrControllerFrame LastFrame)
	{
		SetDiff( ref TriggerPressed, ref TriggerReleased, TriggerIsDown, LastFrame!=null && LastFrame.TriggerIsDown );
		SetDiff( ref TouchpadPressed, ref TouchpadReleased, TouchpadIsDown, LastFrame!=null && LastFrame.TouchpadIsDown );
		SetDiff( ref TouchpadClickPressed, ref TouchpadClickReleased, TouchpadClickIsDown, LastFrame!=null && LastFrame.TouchpadClickIsDown );
		SetDiff( ref AppButtonPressed, ref AppButtonReleased, AppButtonIsDown, LastFrame!=null && LastFrame.AppButtonIsDown );
		SetDiff( ref GripButtonPressed, ref GripButtonReleased, GripButtonIsDown, LastFrame!=null && LastFrame.GripButtonIsDown );
	}

	void SetDiff(ref bool Pressed,ref bool Released,bool Down,bool LastDown)
	{
		Pressed = Down && !LastDown;
		Released = !Down && LastDown;
	}

}



[System.Serializable]
public class OpenvrLighthouseFrame
{
	public bool			Attached;
	public bool			Tracking;

	public Vector3		Position;
	public Quaternion	Rotation;

	public bool IsKeyFrame()
	{
		return false;
	}
}

[System.Serializable]
public class UnityEvent_OpenvrControllerFrames : UnityEvent <List<OpenvrControllerFrame>> {}

[System.Serializable]
public class UnityEvent_OpenvrLighthouseFrame : UnityEvent <List<OpenvrLighthouseFrame>> {}




[ExecuteInEditMode]
public class OpenvrControllerManager : MonoBehaviour {

	public bool									UpdateInEditor = false;

	public UnityEvent_OpenvrControllerFrames	OnUpdateAll;
	public UnityEvent_OpenvrLighthouseFrame		OnUpdateLighthouses;
	public ETrackingUniverseOrigin				TrackingOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;
	List<OpenvrControllerFrame>					LastControllerFrames;		
	int											PeakControllers	{	get {	return LastControllerFrames!=null ? LastControllerFrames.Count : 0; }	}
	CVRSystem									system = null;
	public bool									UsePredictedPoses = true;
	[Range(0,4)]
	public float								PredictedPosePhotoDelay = 0;

	public bool									DebugState = false;

	//	we allow frame injection from external sources via callbacks, which can modify what we send out
	public UnityEvent_OpenvrControllerFrames	OnPreUpdateAll;

	[Header("Certain errors cause unity/app to lock up when init fails. Add reconnect to IPC delay")]
	public float ReInitialiseDelaySecs = 5;
	float? LastInitTime = null;
	float TimeSinceLastInit	{ get { return LastInitTime.HasValue ? Time.time - LastInitTime.Value : 999; }}
	bool CanTryReinit { get { return LastInitTime.HasValue ? TimeSinceLastInit > ReInitialiseDelaySecs : true; } }

	//	from SteamVr
	private static float _copysign(float sizeval, float signval)
	{
		return Mathf.Sign(signval) == 1 ? Mathf.Abs(sizeval) : -Mathf.Abs(sizeval);
	}

	//	from SteamVr
	public static Quaternion GetRotation(Matrix4x4 matrix)
	{
		Quaternion q = new Quaternion();
		q.w = Mathf.Sqrt(Mathf.Max(0, 1 + matrix.m00 + matrix.m11 + matrix.m22)) / 2;
		q.x = Mathf.Sqrt(Mathf.Max(0, 1 + matrix.m00 - matrix.m11 - matrix.m22)) / 2;
		q.y = Mathf.Sqrt(Mathf.Max(0, 1 - matrix.m00 + matrix.m11 - matrix.m22)) / 2;
		q.z = Mathf.Sqrt(Mathf.Max(0, 1 - matrix.m00 - matrix.m11 + matrix.m22)) / 2;
		q.x = _copysign(q.x, matrix.m21 - matrix.m12);
		q.y = _copysign(q.y, matrix.m02 - matrix.m20);
		q.z = _copysign(q.z, matrix.m10 - matrix.m01);
		return q;
	}

	//	from SteamVr
	public static Vector3 GetPosition(Matrix4x4 matrix)
	{
		var x = matrix.m03;
		var y = matrix.m13;
		var z = matrix.m23;

		return new Vector3(x, y, z);
	}

	//	from SteamVr
	public static void RigidTransform(HmdMatrix34_t pose,ref Vector3 Position,ref Quaternion Rotation)
	{
		var m = Matrix4x4.identity;

		m[0, 0] =  pose.m0;
		m[0, 1] =  pose.m1;
		m[0, 2] = -pose.m2;
		m[0, 3] =  pose.m3;

		m[1, 0] =  pose.m4;
		m[1, 1] =  pose.m5;
		m[1, 2] = -pose.m6;
		m[1, 3] =  pose.m7;

		m[2, 0] = -pose.m8;
		m[2, 1] = -pose.m9;
		m[2, 2] =  pose.m10;
		m[2, 3] = -pose.m11;

		Position = GetPosition(m);
		Rotation = GetRotation(m);
	}

	static void SetButton(ulong State,EVRButtonId Button,ref bool Value)
	{
		var ButtonBit = (ulong)((ulong)1 << (int)Button);
		var Down = State & ButtonBit;
		Value = (Down!=0);
	}

	OpenvrControllerFrame GetFrame(VRControllerState_t? pState,TrackedDevicePose_t? pPose,OpenvrControllerFrame LastFrame)
	{
		var Frame = new OpenvrControllerFrame();

		if ( !pState.HasValue || !pPose.HasValue )
		{
			//	if last frame is valid, get a "all-released" frame diff before we return null
			Frame.Attached = false;
			return Frame;
		}

		var State = pState.Value;
		var Pose = pPose.Value;

		Frame.Attached = true;
		Frame.Tracking = Pose.bPoseIsValid;
		RigidTransform( Pose.mDeviceToAbsoluteTracking, ref Frame.Position, ref Frame.Rotation );

		SetButton( State.ulButtonPressed, EVRButtonId.k_EButton_ApplicationMenu, ref Frame.AppButtonIsDown );
		SetButton( State.ulButtonPressed, EVRButtonId.k_EButton_Grip, ref Frame.GripButtonIsDown );
		SetButton( State.ulButtonPressed, EVRButtonId.k_EButton_SteamVR_Touchpad, ref Frame.TouchpadClickIsDown );
		SetButton( State.ulButtonPressed, EVRButtonId.k_EButton_SteamVR_Trigger, ref Frame.TriggerIsDown );

		SetButton( State.ulButtonTouched, EVRButtonId.k_EButton_ApplicationMenu, ref Frame.TouchpadIsDown );

		//	gr: this is what the docs say, but... touchpad seems to be 0
		//		I guess it's k_eControllerAxis_ -1
		//	EVRControllerAxisType	{
		//k_eControllerAxis_None = 0,
		//k_eControllerAxis_TrackPad = 1,
		//k_eControllerAxis_Joystick = 2,
		//k_eControllerAxis_Trigger = 3,

		Frame.TouchpadAxis = new Vector2( State.rAxis0.x, -State.rAxis0.y );
		Frame.TriggerAxis = State.rAxis1.x;

		Frame.CalculateDiff(LastFrame);

		return Frame;
	}

	OpenvrLighthouseFrame GetLighthouseFrame(TrackedDevicePose_t? pPose)
	{
		var Frame = new OpenvrLighthouseFrame();

		if ( !pPose.HasValue )
		{
			//	if last frame is valid, get a "all-released" frame diff before we return null
			Frame.Attached = false;
			return Frame;
		}

		var Pose = pPose.Value;

		Frame.Attached = true;
		Frame.Tracking = Pose.bPoseIsValid;
		RigidTransform( Pose.mDeviceToAbsoluteTracking, ref Frame.Position, ref Frame.Rotation );


		return Frame;
	}

	void Start()
	{
		var ControllerTrackers = UpdateCalibrationShader.FindObjectsOfTypeIncludingDisabled<ControllerTracker>();
		foreach ( var c in ControllerTrackers )
		{
			OnUpdateAll.AddListener(c.UpdatePosition);
		}
	}
 

	void Update()
	{
		if (!UpdateInEditor && Application.isEditor && !Application.isPlaying)
			return;

		if ( system == null )
		{
			//	delayed re-init
			if ( !CanTryReinit )
				return;

			EVRInitError Error = EVRInitError.None;
			system = OpenVR.Init( ref Error, EVRApplicationType.VRApplication_Other );
			LastInitTime = Time.time;
			if ( system == null )
			{
				Debug.LogError("No vr system: " + Error );
				return;
			}
		}
		var sys = system;

		var MaxDevices = 16;	//	find const in API

		var ControllerFrames = new List<OpenvrControllerFrame>();
		var LighthouseFrames = new List<OpenvrLighthouseFrame>();
		var ValidCount = 0;

		var PredictedPoses = new TrackedDevicePose_t[MaxDevices];
		sys.GetDeviceToAbsoluteTrackingPose (TrackingOrigin, PredictedPosePhotoDelay, PredictedPoses);

		var ControllerDeviceIndexes = new List<uint>();
		var LighthouseDeviceIndexes = new List<uint>();
		for ( uint i=0;	i<MaxDevices;	i++ )
		{
			var Type = (sys!=null) ? sys.GetTrackedDeviceClass( i ) : ETrackedDeviceClass.Invalid;
			if (Type == ETrackedDeviceClass.Controller || Type == ETrackedDeviceClass.GenericTracker) {
				ControllerDeviceIndexes.Add (i);
			} else if (Type == ETrackedDeviceClass.TrackingReference) {
				LighthouseDeviceIndexes.Add (i);
			}
		}

		if (DebugState) {
			Debug.Log ("Found " + ControllerDeviceIndexes.Count + " controllers and " + LighthouseDeviceIndexes.Count + " lighthouses");
		}

		foreach ( var i in ControllerDeviceIndexes )
		{
			var State = new VRControllerState_t();
			var StateSize = (uint)Marshal.SizeOf (State);
			var Pose = new TrackedDevicePose_t();
			OpenvrControllerFrame LastFrame = null;
			try
			{
				LastFrame = LastControllerFrames[ControllerFrames.Count];
			}
			catch { }

			var Attached = sys.GetControllerStateWithPose( TrackingOrigin, i, ref State, StateSize, ref Pose );

			if (UsePredictedPoses)
				Pose = PredictedPoses [i];

			var Frame = Attached ? GetFrame( State, Pose, LastFrame ) : GetFrame(null,null,LastFrame);
			ControllerFrames.Add( Frame );

			if ( Attached || LastFrame!=null )
				ValidCount = (int)i+1;
		}

		foreach ( var i in LighthouseDeviceIndexes )
		{
			var State = new VRControllerState_t();
			var StateSize = (uint)Marshal.SizeOf (State);
			TrackedDevicePose_t Pose = new TrackedDevicePose_t ();
			Pose.bPoseIsValid = false;

			if (UsePredictedPoses)
				Pose = PredictedPoses [i];

			var Frame = GetLighthouseFrame (Pose);
			LighthouseFrames.Add( Frame );
		}

		OnPreUpdateAll.Invoke( ControllerFrames );
		OnUpdateAll.Invoke( ControllerFrames );
		OnUpdateLighthouses.Invoke( LighthouseFrames );
		LastControllerFrames = ControllerFrames;
	}


}
