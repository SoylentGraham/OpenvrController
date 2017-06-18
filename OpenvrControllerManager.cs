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

	public bool IsKeyFrame()
	{
		return TriggerPressed || TriggerReleased ||
			TouchpadPressed || TouchpadReleased ||
			TouchpadClickPressed || TouchpadClickReleased ||
			AppButtonPressed || AppButtonReleased;
	}

}

[System.Serializable]
public class UnityEvent_OpenvrControllerFrames : UnityEvent <List<OpenvrControllerFrame>> {}




[ExecuteInEditMode]
public class OpenvrControllerManager : MonoBehaviour {

	public UnityEvent_OpenvrControllerFrames	OnUpdateAll;
	public ETrackingUniverseOrigin				TrackingOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;
	int											PeakControllers = 0;
	CVRSystem									system = null;

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

	OpenvrControllerFrame GetFrame(VRControllerState_t? pState,TrackedDevicePose_t? pPose)
	{
		var Frame = new OpenvrControllerFrame();

		if ( !pState.HasValue || !pPose.HasValue )
		{
			Frame.Attached = false;
			return Frame;
		}

		var State = pState.Value;
		var Pose = pPose.Value;

		Frame.Attached = true;
		Frame.Tracking = Pose.bPoseIsValid;
		RigidTransform( Pose.mDeviceToAbsoluteTracking, ref Frame.Position, ref Frame.Rotation );

		return Frame;
	}



	void Update()
	{
		if ( system == null )
		{
			EVRInitError Error = EVRInitError.None;
			system = OpenVR.Init( ref Error, EVRApplicationType.VRApplication_Other );
			if ( system == null )
			{
				Debug.LogError("No vr system: " + Error );
				return;
			}
		}
		var sys = system;

		var MaxDevices = 16;	//	find const in API

		var Frames = new List<OpenvrControllerFrame>();
		var ValidCount = 0;

		

		var ControllerDeviceIndexes = new List<uint>();
		for ( uint i=0;	i<MaxDevices;	i++ )
		{
			var Type = (sys!=null) ? sys.GetTrackedDeviceClass( i ) : ETrackedDeviceClass.Invalid;
			if ( Type != ETrackedDeviceClass.Controller )
				continue;

			ControllerDeviceIndexes.Add(i);
		}

		foreach ( var i in ControllerDeviceIndexes )
		{
			var State = new VRControllerState_t();
			var Pose = new TrackedDevicePose_t();
			
			var Attached = (sys!=null) ? sys.GetControllerStateWithPose( TrackingOrigin, i, ref State, ref Pose ) : false;
			var Frame = Attached ? GetFrame( State, Pose ) : GetFrame(null,null);
			Frames.Add( Frame );

			if ( Attached )
				ValidCount = (int)i+1;
		}

		//	only send max controllers we've ever had. so we can detect new ones.
		PeakControllers = Mathf.Max( ValidCount, PeakControllers );
		while ( Frames.Count > PeakControllers )
			Frames.RemoveAt( Frames.Count-1 );

		OnUpdateAll.Invoke( Frames );;
	}


}
