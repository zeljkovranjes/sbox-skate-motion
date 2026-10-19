using SkateMotion.Core.Physics;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

/// <summary>Validated canonical settings connected to construction, joint drives and body modes.</summary>
internal sealed class PhysicalRigSettingsAsset
{
	private readonly PhysicalRigBoneSettings[] _bones = new PhysicalRigBoneSettings[ 24 ];
	private readonly PhysicalStructuralJointOverride[] _overrides = new PhysicalStructuralJointOverride[ 22 ];
	private readonly PhysicalJointRangeScales[] _ranges = new PhysicalJointRangeScales[ 22 ];
	private readonly Vector3[] _contacts = new Vector3[ 3 ];
	private readonly byte[] _feedbackFlags = new byte[ 24 ];
	private readonly float[] _feedbackWeights = new float[ 24 ];
	private readonly PhysicalJointPreset[] _drivePresets;

	internal ReadOnlySpan<PhysicalRigBoneSettings> Bones => _bones;
	internal ReadOnlySpan<PhysicalJointRangeScales> Ranges => _ranges;
	internal ReadOnlySpan<Vector3> Contacts => _contacts;
	internal PhysicalRigBodyTuning? BodyConstruction { get; }
	internal PhysicalCompoundBodyDefinition? CompoundBody { get; }
	internal PhysicalStructuralJointTuning? JointConstruction { get; }
	internal PhysicalJointResponseTuning? JointResponse { get; }

	internal PhysicalRigSettingsAsset( PhysicalRigSettingsDefinition definition,
		EffectorRigBoneDefinition[] bindings, Vector3[] dimensions )
	{
		ArgumentNullException.ThrowIfNull( definition );
		ArgumentNullException.ThrowIfNull( bindings );
		ArgumentNullException.ThrowIfNull( dimensions );
		if ( definition.Version != 1 || definition.Bones?.Length != 24 || bindings.Length != 24
			|| dimensions.Length != 24 || definition.Joints?.Length != 22 || definition.Contacts?.Length != 3 )
			throw new ArgumentException( "Physical settings require version 1, 24 canonical bones, 22 ranges and three contacts." );
		for ( var i = 0; i < 24; ++i )
		{
			var bone = definition.Bones[ i ] ?? throw new ArgumentException( "Physical bone settings are missing." );
			if ( string.IsNullOrEmpty( bone.ClipBone ) || bindings[ i ] is null
				|| !string.Equals( bone.ClipBone, bindings[ i ].ClipBone, StringComparison.OrdinalIgnoreCase ) )
				throw new ArgumentException( "Physical settings must match canonical bind order." );
			if ( bone.BodyFlags?.Length != 2 || bone.BodyFlags[ 0 ] is < 0 or > 255
				|| bone.BodyFlags[ 1 ] is < 0 or > 255 || (i != 0 && bone.Shape is not (0 or 1 or 2)) )
				throw new ArgumentException( "Invalid body flags or primitive shape." );
			Validate( bone.DriveCoefficients, 4 );
			Finite( bone.PrimaryMassCoefficient );
			Finite( bone.AlternateMassCoefficient );
			Finite( bone.SizeScale );
			Finite( bone.DriveValue );
			Finite( bone.FirstWeight );
			Finite( bone.SecondWeight );
			Finite( bone.FeedbackWeight );
			Finite( dimensions[ i ].X );
			Finite( dimensions[ i ].Y );
			Finite( dimensions[ i ].Z );
			if ( bone.PrimaryMassCoefficient <= 0f || bone.AlternateMassCoefficient <= 0f || bone.SizeScale <= 0f
				|| dimensions[ i ].X < 0f || dimensions[ i ].Y < 0f || dimensions[ i ].Z < 0f )
				throw new ArgumentException( "Mass and size coefficients must be positive and dimensions nonnegative." );
			var body = new PhysicalRigBodyDefinition( bone.Shape, dimensions[ i ], bone.SizeScale,
				bone.PrimaryMassCoefficient, bone.AlternateMassCoefficient, i == 0 );
			_bones[ i ] = new( body, (byte)bone.BodyFlags[ 0 ], (byte)bone.BodyFlags[ 1 ], bone.BodyValue,
				new Vector4( bone.DriveCoefficients[ 0 ], bone.DriveCoefficients[ 1 ], bone.DriveCoefficients[ 2 ], bone.DriveCoefficients[ 3 ] ),
				bone.DriveValue, bone.FirstWeight, bone.SecondWeight, bone.FeedbackFlag, bone.FeedbackWeight );
			_feedbackFlags[ i ] = bone.FeedbackFlag;
			_feedbackWeights[ i ] = bone.FeedbackWeight;
		}
		for ( var i = 0; i < 22; ++i )
		{
			var joint = definition.Joints[ i ] ?? throw new ArgumentException( "Physical joint settings are missing." );
			Validate( joint.Ranges, 4 );
			_overrides[ i ] = new( joint.Locked, joint.Ranges[ 0 ], joint.Ranges[ 1 ] );
			_ranges[ i ] = new( joint.Ranges[ 0 ], joint.Ranges[ 1 ], joint.Ranges[ 2 ], joint.Ranges[ 3 ] );
		}
		for ( var i = 0; i < 3; ++i )
		{
			var values = definition.Contacts[ i ];
			Validate( values, 3 );
			_contacts[ i ] = new( values[ 0 ], values[ 1 ], values[ 2 ] );
		}
		if ( definition.JointResponse is { } response )
		{
			Finite( response.VelocityGain );
			Finite( response.PositionGain );
			JointResponse = response;
		}
		if ( definition.DrivePresets is { } presets )
		{
			if ( presets.Length != 2 )
				throw new ArgumentException( "Physical drive construction requires two presets." );
			foreach ( var preset in presets )
			{
				Finite( preset.FirstPosition );
				Finite( preset.FirstVelocity );
				Finite( preset.SecondPosition );
				Finite( preset.SecondVelocity );
			}
			_drivePresets = presets.AsSpan().ToArray();
		}
		if ( definition.JointConstruction is { } jointTuning )
		{
			Validate( jointTuning.LinearSpeed, 4 );
			Finite( jointTuning.SwingSpeed );
			Finite( jointTuning.TwistSpeed );
			JointConstruction = new( new Vector4( jointTuning.LinearSpeed[ 0 ], jointTuning.LinearSpeed[ 1 ],
				jointTuning.LinearSpeed[ 2 ], jointTuning.LinearSpeed[ 3 ] ), jointTuning.SwingSpeed,
				jointTuning.TwistSpeed, jointTuning.LockSwing, jointTuning.LockTwist );
		}
		if ( definition.BodyConstruction is { } construction )
		{
			ValidateConstruction( construction );
			BodyConstruction = construction;
		}
		if ( definition.CompoundBody is { } compound )
		{
			if ( BodyConstruction is not { } tuning )
				throw new ArgumentException( "Compound metadata requires body construction settings." );
			Validate( compound.Angles, 3 );
			Validate( compound.Position, 3 );
			Finite( compound.CylinderHeight );
			Finite( compound.CylinderRadius );
			if ( compound.CylinderHeight < 0f || compound.CylinderRadius <= 0f )
				throw new ArgumentException( "Compound cylinder dimensions are invalid." );
			CompoundBody = new( new( compound.Angles[ 0 ], compound.Angles[ 1 ], compound.Angles[ 2 ] ),
				new( compound.Position[ 0 ], compound.Position[ 1 ], compound.Position[ 2 ] ),
				dimensions[ 1 ], _bones[ 1 ].Body.SizeScale, compound.CylinderHeight, compound.CylinderRadius, tuning.MassScale );
		}
	}

	internal PhysicalRigBodyAssembly CreateBodies( bool compound, LimbSolverMath math, Vector3 defaultAcceleration ) =>
		PhysicalRigBodyAssembly.Create( this, compound, math, defaultAcceleration );

	internal PhysicalRigTargetAssembly CreateTargets( Vector3 defaultAcceleration, int accumulatorState )
	{
		if ( BodyConstruction is not { } tuning )
			throw new InvalidOperationException( "Authored body construction settings are missing." );
		return new( tuning.AxisTolerance, defaultAcceleration, accumulatorState );
	}

	internal PhysicalJointPreset[] CreateDrivePresets()
	{
		if ( _drivePresets is null )
			throw new InvalidOperationException( "Authored drive presets are missing." );
		return _drivePresets.AsSpan().ToArray();
	}

	internal PhysicalJointState[] CreateCanonicalJoints( EffectorSkeletonHierarchy hierarchy )
	{
		ArgumentNullException.ThrowIfNull( hierarchy );
		if ( JointResponse is not { } response )
			throw new InvalidOperationException( "Authored joint response settings are missing." );
		var joints = new PhysicalJointState[ 24 ];
		for ( var i = 0; i < joints.Length; ++i )
			joints[ i ] = new()
			{
				ChildBone = -1, VelocityGain = response.VelocityGain, PositionGain = response.PositionGain,
				FirstWeight = _bones[ i ].FirstWeight, SecondWeight = _bones[ i ].SecondWeight
			};
		PhysicalJointInitialization.Initialize( joints, hierarchy.InitialBones, hierarchy.Parents, CreateDrivePresets() );
		return joints;
	}

	internal PhysicalStructuralJoint[] BuildAuthoredJoints( PhysicalStructuralRigAsset structural,
		EffectorSkeletonHierarchy hierarchy, LimbSolverMath math )
	{
		if ( JointConstruction is not { } tuning )
			throw new InvalidOperationException( "Authored joint construction settings are missing." );
		return BuildJoints( structural, hierarchy, tuning, math );
	}

	internal PhysicalStructuralJoint[] BuildJoints( PhysicalStructuralRigAsset structural,
		EffectorSkeletonHierarchy hierarchy, PhysicalStructuralJointTuning tuning, LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( structural );
		return structural.Build( hierarchy, _overrides, tuning, math );
	}

	internal PhysicalRigBodyModes CreateBodyModes( PhysicalRigModeBody[] bodies, bool suppressAll ) =>
		new( bodies, _contacts, suppressAll );

	internal void EnterPrimaryFeedback( PhysicalRigFeedbackState feedback )
	{
		ArgumentNullException.ThrowIfNull( feedback );
		feedback.EnterPrimary( _feedbackFlags, _feedbackWeights );
	}

	internal void ApplyJointWeights( PhysicalJointState[] joints )
	{
		ArgumentNullException.ThrowIfNull( joints );
		foreach ( var joint in joints )
		{
			if ( joint is null || joint.ChildBone < 0 || joint.ChildBone >= 24 )
				throw new ArgumentException( "Joint drive weights require canonical child indices." );
		}
		foreach ( var joint in joints )
		{
			joint.FirstWeight = _bones[ joint.ChildBone ].FirstWeight;
			joint.SecondWeight = _bones[ joint.ChildBone ].SecondWeight;
		}
	}

	private static void Validate( float[] values, int count )
	{
		if ( values?.Length != count )
			throw new ArgumentException( "Physical settings component count is invalid." );
		foreach ( var value in values )
			Finite( value );
	}

	private static void Finite( float value )
	{
		if ( !float.IsFinite( value ) )
			throw new ArgumentException( "Physical settings must be finite." );
	}

	private static void ValidateConstruction( PhysicalRigBodyTuning tuning )
	{
		var values = new[] { tuning.RootRadius, tuning.RootHalfLength, tuning.MassScale, tuning.AlternateMassScale,
			tuning.CapsuleRadiusScale, tuning.CapsuleLengthScale, tuning.InertiaScale, tuning.AxisTolerance };
		foreach ( var value in values )
		{
			if ( !float.IsFinite( value ) || value <= 0f )
				throw new ArgumentException( "Body construction scales and tolerance must be finite and positive." );
		}
		if ( tuning.InertiaMode is < 0 or > 2 )
			throw new ArgumentException( "Body construction inertia mode is unsupported." );
	}
}
