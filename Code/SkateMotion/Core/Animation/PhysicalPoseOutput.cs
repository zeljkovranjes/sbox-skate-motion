namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Builds physical attachment frames and drive coefficients from an evaluated rigid pose.</summary>
internal static class PhysicalPoseOutput
{
	private const float VelocityScale = 59.999996185302734f;
	private const float PositionScale = 3599.99951171875f;

	internal static PhysicalJointDrive Update( LimbRigidTransform[] poses, LimbRigidTransform[] inverses,
		PhysicalJointState[] joints, byte[] blendMask, PhysicalPoseSettings settings, PhysicalJointPreset[] presets )
	{
		if ( poses.Length != inverses.Length || blendMask.Length <= joints.Length || presets.Length != 2 )
			throw new ArgumentException( "Pose storage, joint mask and two drive presets must match their inputs." );

		// Bone zero is reserved. Its caller-owned inverse remains untouched.
		for ( var i = 1; i < poses.Length; i++ )
			inverses[ i ] = poses[ i ].RigidInverse();

		for ( var i = 0; i < joints.Length; i++ )
		{
			var joint = joints[ i ];
			var blended = settings.Blend < 1f && blendMask[ i + 1 ] != 0;
			joint.Mode = blended ? 3 : 0;
			if ( joint.ChildBone == 0 )
				continue;

			joint.FirstStrength = Strength( settings.FirstStrength, joint.FirstWeight, settings, blended );
			joint.SecondStrength = Strength( settings.SecondStrength, joint.SecondWeight, settings, blended );
			var child = inverses[ joint.ChildBone ];
			var anchor = poses[ joint.ChildBone ].Translation;
			if ( joint.FirstEnabled )
			{
				joint.FirstFrame = Frame( child, inverses[ joint.FirstBodyBone ], anchor );
				(joint.FirstConstraint, joint.SecondConstraint) = Drives( joint, presets[ 0 ], joint.FirstStrength );
				joint.Dirty = false;
			}
			if ( joint.SecondEnabled )
			{
				joint.SecondFrame = Frame( child, inverses[ joint.SecondBodyBone ], anchor );
				(joint.ThirdConstraint, joint.FourthConstraint) = Drives( joint, presets[ 1 ], joint.SecondStrength );
				joint.Dirty = false;
			}
		}

		return new PhysicalJointDrive( 599.9999389648438f, 0f, 35999.99609375f, 2 );
	}

	private static float Strength( float strength, float weight, PhysicalPoseSettings settings, bool blended )
	{
		// The mask-selected branch uses authored strength even when the fallback pair is disabled.
		return blended ? strength * MultiplyAdd( settings.Blend, 1f - weight, weight )
			: settings.Enabled ? strength : 0f;
	}

	private static (PhysicalJointDrive First, PhysicalJointDrive Second) Drives(
		PhysicalJointState joint, PhysicalJointPreset preset, float strength )
	{
		if ( joint.Mode == 0 )
			return (new( preset.FirstVelocity * VelocityScale, 0f, preset.FirstPosition * PositionScale, 2 ),
				new( preset.SecondVelocity * VelocityScale, 0f, preset.SecondPosition * PositionScale, 2 ));

		return (new( 0f, 0f, 0f, 1 ),
			new( joint.VelocityGain * strength, 200f, strength * (joint.PositionGain * PositionScale), 1 ));
	}

	private static PhysicalJointFrame Frame( LimbRigidTransform child, LimbRigidTransform body, Vector3 anchor ) => new(
		Rotation( child.Rotation ), child.Transform( anchor ), Rotation( body.Rotation ), body.Transform( anchor ) );

	internal static Vector4 Rotation( LimbFrame frame )
	{
		var x = frame.X;
		var y = frame.Y;
		var z = frame.Z;
		float diagonal;
		int lane;
		Vector4 terms;
		if ( (x.X + y.Y) + z.Z > 0f )
		{
			lane = 3;
			diagonal = (x.X + y.Y) + (1f + z.Z);
			terms = new( y.Z - z.Y, z.X - x.Z, x.Y - y.X, .5f );
		}
		else if ( x.X > y.Y && x.X > z.Z )
		{
			lane = 0;
			diagonal = (x.X - y.Y) + (1f - z.Z);
			terms = new( .5f, x.Y + y.X, z.X + x.Z, y.Z - z.Y );
		}
		else if ( y.Y > z.Z )
		{
			lane = 1;
			diagonal = (-x.X + y.Y) + (1f - z.Z);
			terms = new( x.Y + y.X, .5f, y.Z + z.Y, z.X - x.Z );
		}
		else
		{
			lane = 2;
			diagonal = (-x.X - y.Y) + (1f + z.Z);
			terms = new( z.X + x.Z, y.Z + z.Y, .5f, x.Y - y.X );
		}

		var inverse = InverseSquareRoot( diagonal );
		var major = MultiplyAdd( diagonal, inverse, 0f );
		var minor = MultiplyAdd( .5f, inverse, 0f );
		return new Vector4(
			MultiplyAdd( terms.X, lane == 0 ? major : minor, 0f ),
			MultiplyAdd( terms.Y, lane == 1 ? major : minor, 0f ),
			MultiplyAdd( terms.Z, lane == 2 ? major : minor, 0f ),
			MultiplyAdd( terms.W, lane == 3 ? major : minor, 0f ) );
	}
}
