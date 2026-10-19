using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Owns coordinated rig mode changes and their registered structural joints.</summary>
internal sealed class PhysicalRigModeController
{
	private readonly PhysicalSolverWorld _world;
	private readonly PhysicalRigState _rig;
	private readonly PhysicalRigBodyModes _bodies;
	private readonly PhysicalRigFeedbackState _feedback;
	private readonly PhysicalStructuralRigBinding _binding;
	private readonly PhysicalStructuralJoint[] _joints;
	private readonly PhysicalStructuralBone[] _bones;
	private readonly PhysicalJointRangeScales[] _scales;
	private readonly byte[] _authoredFlags;
	private readonly float[] _authoredWeights;
	private readonly LimbSolverMath _math;
	private bool _released;

	internal ReadOnlySpan<PhysicalStructuralJoint> Joints => _joints;

	internal PhysicalRigModeController( PhysicalSolverWorld world, PhysicalRigState rig,
		PhysicalRigBodyModes bodies, PhysicalRigFeedbackState feedback, PhysicalStructuralBone[] bones,
		PhysicalStructuralJoint[] joints, PhysicalJointRangeScales[] scales, byte[] authoredFlags,
		float[] authoredWeights, LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( world );
		ArgumentNullException.ThrowIfNull( rig );
		ArgumentNullException.ThrowIfNull( bodies );
		ArgumentNullException.ThrowIfNull( feedback );
		ArgumentNullException.ThrowIfNull( bones );
		ArgumentNullException.ThrowIfNull( joints );
		ArgumentNullException.ThrowIfNull( scales );
		ArgumentNullException.ThrowIfNull( authoredFlags );
		ArgumentNullException.ThrowIfNull( authoredWeights );
		ArgumentNullException.ThrowIfNull( math );
		if ( bones.Length != 24 || authoredFlags.Length != 24 || authoredWeights.Length != 24
			|| scales.Length < joints.Length || rig.Bodies.Length != 26 || rig.FeedbackState is not null )
			throw new ArgumentException( "Mode ownership requires a complete unbound canonical rig and matching authored settings." );
		_world = world;
		_rig = rig;
		_bodies = bodies;
		_feedback = feedback;
		_math = math;
		ValidateBodies();
		_joints = joints.AsSpan().ToArray();
		_bones = bones.AsSpan().ToArray();
		_scales = scales.AsSpan().ToArray();
		_authoredFlags = authoredFlags.AsSpan().ToArray();
		_authoredWeights = authoredWeights.AsSpan().ToArray();
		_binding = new( world, rig.Bodies, _joints );
		rig.FeedbackState = feedback;
	}

	internal void ApplyPrimary( bool enableCollisions )
	{
		ValidateBodies();
		RefreshRanges( false, 1f, 1f );
		_bodies.ApplyPrimary( enableCollisions );
		_feedback.EnterPrimary( _authoredFlags, _authoredWeights );
		_world.RefreshRigBodyProperties( _bodies );
	}

	internal void ApplySecondary( bool primaryLimits, PhysicalRigSecondaryMode settings,
		float swingMultiplier, float twistMultiplier )
	{
		ValidateBodies();
		RefreshRanges( !primaryLimits, swingMultiplier, twistMultiplier );
		_bodies.ApplySecondary( settings );
		_world.RefreshRigBodyProperties( _bodies );
	}

	internal void Release()
	{
		if ( _released )
			return;
		_binding.Release();
		if ( ReferenceEquals( _rig.FeedbackState, _feedback ) )
			_rig.FeedbackState = null;
		_released = true;
	}

	private void ValidateBodies()
	{
		if ( _released )
			throw new InvalidOperationException( "A released rig no longer owns mode transitions." );
		if ( _binding is not null && !ReferenceEquals( _rig.FeedbackState, _feedback ) )
			throw new InvalidOperationException( "The rig feedback state no longer belongs to this mode controller." );
		for ( var i = 0; i < 26; ++i )
			if ( !ReferenceEquals( _rig.Bodies[ i ]?.Body, _bodies.Bodies[ i ].Body ) )
				throw new ArgumentException( "Mode bodies must match the rig's canonical physical identities." );
		_world.ValidateRigBodyProperties( _bodies );
	}

	private void RefreshRanges( bool alternate, float swingMultiplier, float twistMultiplier )
	{
		for ( var i = 0; i < _joints.Length; ++i )
		{
			_joints[ i ] = PhysicalStructuralJointRanges.Refresh( _joints[ i ], _bones[ _joints[ i ].ChildBone ],
				_scales[ i ], alternate, swingMultiplier, twistMultiplier, _math );
			_binding.UpdateJoint( i, _joints[ i ] );
		}
	}
}
