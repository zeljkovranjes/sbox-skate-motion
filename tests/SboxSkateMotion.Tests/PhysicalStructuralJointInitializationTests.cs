using System;
using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalStructuralJointInitializationTests
{
	[Fact]
	public void MissingMappingsDoNotConsumeAnOverrideSlot()
	{
		var input = new Inputs();
		input.Parents[ 1 ] = 0;
		input.Parents[ 4 ] = 2;
		input.Parents[ 9 ] = 5;
		input.ToClip[ 0 ] = -1;
		input.ToClip[ 2 ] = 31;
		input.FromClip[ 31 ] = 2;
		input.Overrides[ 0 ] = new( true, 7f, 8f );
		input.Overrides[ 1 ] = new( false, 2f, 3f );
		var joints = input.Build();
		Assert.Equal( 2, joints.Length );
		Assert.Equal( (2, 4), (joints[ 0 ].ParentBone, joints[ 0 ].ChildBone) );
		Assert.Equal( 0f, joints[ 0 ].Limits.SwingAngle );
		Assert.Equal( (5, 9), (joints[ 1 ].ParentBone, joints[ 1 ].ChildBone) );
		Assert.Equal( .8f, joints[ 1 ].Limits.SwingAngle );
		Assert.Equal( 1.8000001f, joints[ 1 ].Limits.TwistAngle );
	}

	[Fact]
	public void BodyOffsetsResolveBothAnchorsToTheChildBindPosition()
	{
		var input = new Inputs();
		input.Parents[ 5 ] = 2;
		input.Poses[ 2 ] = LimbRigidTransform.Identity with { Translation = new( 2f, 3f, 4f ) };
		input.Poses[ 5 ] = LimbRigidTransform.Identity with { Translation = new( 5f, 7f, 11f ) };
		input.Bones[ 2 ] = input.Bones[ 2 ] with { BodyPosition = new( .2f, .3f, .4f ) };
		input.Bones[ 5 ] = input.Bones[ 5 ] with { BodyPosition = new( .5f, .6f, .7f ) };
		var joint = Assert.Single( input.Build() );
		var childWorld = input.Poses[ 5 ].Translation + input.Bones[ 5 ].BodyPosition + joint.Frame.ChildPosition;
		var parentWorld = input.Poses[ 2 ].Translation + input.Bones[ 2 ].BodyPosition + joint.Frame.BodyPosition;
		Assert.InRange( Vector3.Distance( childWorld, input.Poses[ 5 ].Translation ), 0f, 1e-5f );
		Assert.InRange( Vector3.Distance( parentWorld, childWorld ), 0f, 1e-5f );
		Assert.Equal( new Vector4( 0f, 0f, 0f, 1f ), joint.LinearRotation );
	}

	[Theory]
	[InlineData( false, false, 1, 1 )]
	[InlineData( true, false, 4, 1 )]
	[InlineData( false, true, 1, 2 )]
	[InlineData( true, true, 4, 2 )]
	public void GlobalLocksKeepTheAuthoredRanges( bool swing, bool twist, int swingMode, int twistMode )
	{
		var input = new Inputs();
		input.Parents[ 1 ] = 0;
		input.Tuning = input.Tuning with { LockSwing = swing, LockTwist = twist };
		var limits = Assert.Single( input.Build() ).Limits;
		Assert.Equal( .4f, limits.SwingAngle );
		Assert.Equal( .6f, limits.TwistAngle );
		Assert.Equal( swingMode, limits.SwingMode );
		Assert.Equal( twistMode, limits.TwistMode );
		Assert.InRange( limits.SwingCosine, .92105f, .92108f );
		Assert.InRange( limits.TwistCosine, .82532f, .82535f );
	}

	[Fact]
	public void APerJointLockClearsAnglesAndRetainsMotionSpeedLimits()
	{
		var input = new Inputs();
		input.Parents[ 1 ] = 0;
		input.Overrides[ 0 ] = new( true, 10f, 20f );
		var limits = Assert.Single( input.Build() ).Limits;
		Assert.Equal( (0f, 0f, 1f, 1f, 4, 2),
			(limits.SwingAngle, limits.TwistAngle, limits.SwingCosine, limits.TwistCosine, limits.SwingMode, limits.TwistMode) );
		Assert.Equal( input.Tuning.LinearSpeed * 59.999996f, limits.LinearSpeed );
		Assert.Equal( 719.99994f, limits.SwingSpeed );
		Assert.Equal( 1079.9999f, limits.TwistSpeed );
	}

	[Fact]
	public void ScaledRangesHaveAMinimumWithoutAnUpperClamp()
	{
		var input = new Inputs();
		input.Parents[ 1 ] = 0;
		input.Overrides[ 0 ] = new( false, -1f, 10f );
		var limits = Assert.Single( input.Build() ).Limits;
		Assert.Equal( .01f, limits.SwingAngle );
		Assert.Equal( 6f, limits.TwistAngle );
	}

	[Fact]
	public void ConstructionRejectsOverflowAndInvalidMappings()
	{
		var input = new Inputs();
		for ( var i = 1; i < 24; ++i )
			input.Parents[ i ] = i - 1;
		Assert.Throws<ArgumentException>( () => input.Build() );
		Array.Fill( input.Parents, -1 );
		input.Parents[ 1 ] = 24;
		Assert.Throws<ArgumentOutOfRangeException>( () => input.Build() );
		input.Parents[ 1 ] = 0;
		input.ToClip[ 0 ] = 36;
		Assert.Throws<ArgumentOutOfRangeException>( () => input.Build() );
		input.ToClip[ 0 ] = 3;
		input.FromClip[ 3 ] = -1;
		Assert.Throws<ArgumentOutOfRangeException>( () => input.Build() );
	}

	private sealed class Inputs
	{
		internal readonly PhysicalStructuralBone[] Bones = new PhysicalStructuralBone[ 24 ];
		internal readonly LimbRigidTransform[] Poses = new LimbRigidTransform[ 24 ];
		internal readonly int[] Parents = new int[ 24 ];
		internal readonly int[] ToClip = new int[ 24 ];
		internal readonly int[] FromClip = new int[ 36 ];
		internal readonly PhysicalStructuralJointOverride[] Overrides = new PhysicalStructuralJointOverride[ 22 ];
		internal PhysicalStructuralJointTuning Tuning = new( new( 2f, 3f, 4f, 5f ), 12f, 18f, false, false );

		internal Inputs()
		{
			var identity = new Vector4( 0f, 0f, 0f, 1f );
			Array.Fill( Bones, new PhysicalStructuralBone( identity, identity, identity, Vector3.Zero, .4f, .6f ) );
			Array.Fill( Poses, LimbRigidTransform.Identity );
			Array.Fill( Parents, -1 );
			Array.Fill( FromClip, -1 );
			Array.Fill( Overrides, new PhysicalStructuralJointOverride( false, 1f, 1f ) );
			for ( var i = 0; i < 24; ++i )
			{
				ToClip[ i ] = i;
				FromClip[ i ] = i;
			}
		}

		internal PhysicalStructuralJoint[] Build()
		{
			var math = new LimbSolverMath( [ -1f / 720f, 1f / 24f, -.5f ],
				[ -1f / 5040f, 1f / 120f, -1f / 6f ], new float[ 8 ],
				2f / MathF.PI, MathF.PI / 2f, 0f, MathF.PI );
			return PhysicalStructuralJointInitialization.Build( Bones, Poses, Parents, ToClip, FromClip, Overrides, Tuning, math );
		}
	}
}
