using System.Numerics;
using SkateMotion.Core.Animation;
using Xunit;

namespace SboxSkateMotion.Tests;

public class PhysicalPoseFeedbackTests
{
	[Fact]
	public void ResetHistoryOccursAfterVelocityAndDirectFeedbackUsesBothTargets()
	{
		var state = new PhysicalPoseFeedbackState { Position = Vector3.UnitX };
		var readback = Readback();
		readback.WeightedPhysicalPosition = new( 3f, 0f, 0f );
		readback.Poses[ 24 ] = Pose( new( 4f, 2f, 1f ) );
		readback.Poses[ 25 ] = Pose( new( -2f, 3f, 1f ) );
		var preparation = new PhysicalPosePreparationState
		{
			FirstFeedbackPosition = new( 1f, 1f, 1f ), SecondFeedbackPosition = new( 0f, 1f, 1f )
		};
		Create().Update( state, readback, preparation, Targets(),
			new( LimbRigidTransform.Identity, Vector3.UnitY, .5f, true, true ), [] );
		Assert.Equal( new Vector3( 4f, 0f, 0f ), state.Velocity );
		Assert.Equal( state.Position, state.PreviousPosition );
		Assert.Equal( new Vector3( 3f, 1f, 0f ), state.FirstFeedback );
		Assert.Equal( new Vector3( -2f, 2f, 0f ), state.SecondFeedback );
		Assert.Equal( state.FirstFeedback, state.Correction );
	}

	[Fact]
	public void FilteredModeClearsDirectFeedbackAndNoContactsRemoveCorrection()
	{
		var state = new PhysicalPoseFeedbackState
		{
			FirstFeedback = Vector3.One, SecondFeedback = Vector3.One, Correction = Vector3.One
		};
		var readback = Readback();
		readback.Poses[ 17 ] = Pose( new( 3f, 4f, 0f ) );
		readback.Poses[ 19 ] = Pose( new( 100f, 0f, 0f ) );
		Create().Update( state, readback, new(), Targets(),
			new( LimbRigidTransform.Identity, Vector3.UnitY, 1f / 60f, false, false ), [] );
		Assert.Equal( Vector3.Zero, state.Correction );
		Assert.Equal( Vector3.Zero, state.FirstFeedback );
		Assert.Equal( Vector3.Zero, state.SecondFeedback );
		Assert.Equal( 5f, state.MaximumError );
	}

	[Fact]
	public void ForwardContactKeepsOnlyTheErrorInItsPlane()
	{
		var value = Create().Filter( new( 3f, 7f, 0f ), Vector3.UnitY, [Vector3.UnitX] );
		Assert.Equal( new Vector3( 3f, 0f, 0f ), value );
	}

	[Fact]
	public void ObliqueContactRotatesAndAttenuatesTheProjectedError()
	{
		var value = Create().Filter( Vector3.UnitX, Vector3.UnitY, [new( 1f, 0f, 1f )] );
		Assert.InRange( value.X, .49999f, .50001f );
		Assert.Equal( 0f, value.Y );
		Assert.InRange( value.Z, .49999f, .50001f );
	}

	[Fact]
	public void ContactBoundsDoNotDependOnEnumerationOrder()
	{
		var first = new Vector3( 1f, 0f, -1f );
		var second = new Vector3( 1f, 0f, 1f );
		var solver = Create();
		var a = solver.Filter( Vector3.UnitX, Vector3.UnitY, [first, second, Vector3.UnitX] );
		var b = solver.Filter( Vector3.UnitX, Vector3.UnitY, [Vector3.UnitX, second, first] );
		Assert.Equal( a, b );
		Assert.InRange( a.X, .99999f, 1.00001f );
	}

	[Fact]
	public void NegligibleProjectionReturnsBeforeContactFiltering()
	{
		var error = new Vector3( .0000005f, 2f, 0f );
		Assert.Equal( new Vector3( error.X, 0f, 0f ), Create().Filter( error, Vector3.UnitY, [] ) );
	}

	[Theory]
	[InlineData( 0f )]
	[InlineData( -1f )]
	[InlineData( float.NaN )]
	public void InvalidCadenceDoesNotMutateRetainedState( float delta )
	{
		var state = new PhysicalPoseFeedbackState { Position = Vector3.One };
		Assert.Throws<ArgumentOutOfRangeException>( () => Create().Update( state, Readback(), new(), Targets(),
			new( LimbRigidTransform.Identity, Vector3.UnitY, delta, false, false ), [] ) );
		Assert.Equal( Vector3.One, state.Position );
	}

	private static LimbRigidTransform Pose( Vector3 position ) => new( LimbRigidTransform.Identity.Rotation, position );
	private static LimbRigidTransform[] Targets() => Enumerable.Repeat( LimbRigidTransform.Identity, 24 ).ToArray();
	private static PhysicalPoseResetState Readback()
	{
		var state = new PhysicalPoseResetState( 26 );
		Array.Fill( state.Poses, LimbRigidTransform.Identity );
		return state;
	}

	private static PhysicalPoseFeedback Create() => new( new LimbSolverMath(
		[-.0013602249091491103f, .04165669530630112f, -.4999990165233612f],
		[-.0001950727018993348f, .00833207555115223f, -.16666652262210846f],
		[-.0012624911032617092f, .006670089904218912f, -.01708812639117241f, .03089188039302826f,
			-.050174303352832794f, .08897899091243744f, -.21459880471229553f, 1.570796251296997f],
		.6366197466850281f, 1.570796251296997f, 7.549790126404332e-8f, 3.1415927410125732f ) );
}
