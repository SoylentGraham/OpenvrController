using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class UnityEvent_Transform_int3 : UnityEvent <Transform,int,int,int> {}

[System.Serializable]
public class UnityEvent_Transform_float3 : UnityEvent<Transform, float, float, float> { }

[System.Serializable]
public class UnityEvent_Position : UnityEvent<Vector3> { }


public class ControllerTracker : MonoBehaviour {


	[Range(0,10)]
	public int			ControllerIndex = 0;

	public UnityEvent_Transform_int3	OnEnter;
	public UnityEvent_Transform_float3	OnMove;
	public UnityEvent_Position			OnTrackingPosition;

	NormalisedCalibration				TheCalibration	{	get{ return GameObject.FindObjectOfType<NormalisedCalibration> (); }}

	Bounds?		LastBounds;

	[System.NonSerialized]
	public bool GripDown = false;

	void SetNewBounds(Bounds Box,int x,int y,int z)
	{
		LastBounds = Box;
		OnEnter.Invoke (this.transform, x, y, z);
	}

	void SetNoBounds()
	{
		LastBounds = null;
	}

	void FindNewBounds(Vector3 Position)
	{
		/*
		var Calibration = TheCalibration;
		for (int y = 0;	y < Calibration.SpaceHeight;	y++) {
			for (int x = 0;	x < Calibration.SpaceColumns;	x++) {
				for (int z = 0;	z < Calibration.SpaceRows;	z++) {
					var Box = Calibration.GetSpaceBox (x, y, z);
					if (!Box.Contains (Position))
						continue;

					SetNewBounds (Box, x, y, z);
					return;
				}
			}
		}
		SetNoBounds ();
		*/
	}

	void Update () 
	{
		var Pos = transform.position;

		if (LastBounds.HasValue) {
			if (!LastBounds.Value.Contains (Pos)) {
				FindNewBounds (Pos);
			}
		} else {
			FindNewBounds (Pos);
		}

		//	get normalised position 
		try
		{
			var Calibration = TheCalibration;
			var PosNormal = Calibration.GetNormalisedPosition( Pos );
			OnMove.Invoke( this.transform, PosNormal.x, PosNormal.y, PosNormal.z );
		}
		catch
		{

		}
	}

	public void UpdatePosition(List<OpenvrControllerFrame> Controllers)
	{
		//	disable self to show no controller on error
		try
		{
			var Controller = Controllers[ControllerIndex];
			this.transform.position = Controller.Position;
			this.transform.rotation = Controller.Rotation;

			//	if not tracking, dont enable as we'll get back positions
			var Tracking = Controller.Attached && Controller.Tracking;
			this.gameObject.SetActive(Tracking);

			GripDown = Controller.GripButtonIsDown;

			if (Tracking)
				OnTrackingPosition.Invoke(this.transform.position);
		}
		catch (System.Exception)
		{
			GripDown = false;
			this.gameObject.SetActive(false);
		}
	}
}
