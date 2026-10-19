using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Vehicle;
using Xunit;

namespace SboxSkateMotion.Tests;

public class ArticulatedBoardGeneratedContactsTests
{
	[Fact]
	public void GeneratedStorageRetainsMoreThanTwentyFourContactsAndTracksActiveCount()
	{
		var board = Board();
		var record = Record( board.Body( 0 ) );
		var records = new uint[ 40 * 64 ];
		for ( var i = 0; i < 40; ++i )
		{
			record.CopyTo( records.AsSpan( i * 64, 64 ) );
			records[ i * 64 + 23 ] = (uint)i;
		}
		Assert.Equal( 40, board.StepGenerated( records, Bindings(), [], 0, 0, .001f ) );
		Assert.Equal( 40, board.ContactCount );
		Assert.Equal( 39u, board.Contacts( board.ContactCount )[ 39 ].Materials );
		Assert.Equal( ArticulatedBoard.StaticMapBodyIndex, board.Contacts( 1 )[ 0 ].BodyB );
		Assert.Equal( 0f, board.StaticMapBody.InverseMass );
		Assert.Equal( 1, board.StaticMapBody.MotionFlags );
		Assert.Equal( 1, board.StepGenerated( record, Bindings(), [], 0, 0, .001f ) );
		Assert.Equal( 1, board.ContactCount );
		Assert.Throws<ArgumentException>( () => board.Step( new CollisionContact[ 25 ], 0, 0, .001f ) );
	}

	[Fact]
	public void CachedGeneratedContactAndGenericSurfaceShareOneBoardIntegration()
	{
		var board = Board();
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; ++i )
			board.Body( i ).Velocity = Vector3.UnitX;
		board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
		var record = Record( board.Body( 0 ) );
		record[ 24 ] = Word( .02f );
		record[ 15 ] = Word( .1f );
		record[ 19 ] = Word( .8f );
		var surface = new CollisionContact( 0, 7, Vector3.Zero, Vector3.Zero,
			Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, .8f, .5f, 0 );
		const float step = .001f;
		Assert.Equal( 2, board.StepGenerated( record, Bindings(), [surface], 0, 0, step ) );
		var contacts = board.Contacts( board.ContactCount );
		Assert.Equal( new Vector3( -.02f, 0, 0 ), contacts[ 0 ].OffsetA );
		Assert.Equal( Vector3.Zero, contacts[ 0 ].TangentA );
		Assert.Equal( .1f, contacts[ 0 ].StaticFriction );
		Assert.Equal( .8f, contacts[ 0 ].SlidingFriction );
		Assert.Equal( 7, contacts[ 1 ].BodyB );
		Assert.InRange( Vector3.Distance( board.Deck.Position, Vector3.UnitX * step ), 0, .000001f );
		Assert.InRange( Vector3.Distance( board.Deck.Velocity, Vector3.UnitX ), 0, .000001f );
	}

	[Fact]
	public void GeneratedPassUsesCachedMassForKineticAndSeparationResponse()
	{
		var bodies = new[] { new SolverBody { InverseMass = 99 }, new SolverBody { InverseMass = 77 } };
		var contacts = new[]
		{
			new ContactConstraint
			{
				BodyA = 0, BodyB = 1, Normal = Vector3.UnitZ,
				InverseMassA = 2, InverseMassB = 3, Bias = new Vector4( 1, 0, 0, 2 )
			}
		};
		ConstraintSolver.SolveGeneratedPass( bodies, contacts, [], [] );
		Assert.Equal( Vector3.UnitZ * 2, bodies[ 0 ].LinearVelocity );
		Assert.Equal( -Vector3.UnitZ * 3, bodies[ 1 ].LinearVelocity );
		Assert.Equal( Vector3.UnitZ * 4, bodies[ 0 ].CorrectionVelocity );
		Assert.Equal( -Vector3.UnitZ * 6, bodies[ 1 ].CorrectionVelocity );
		Assert.Equal( 99, bodies[ 0 ].InverseMass );
		Assert.Equal( 77, bodies[ 1 ].InverseMass );
	}

	private static BoardContactBodyBinding[] Bindings() => [new( 1, 0 ), new( 8, ArticulatedBoard.StaticMapBodyIndex )];

	private static ArticulatedBoard Board()
	{
		var board = new ArticulatedBoard( new SkateSettings(),
			[new( .26f, .095f, -.0665f ), new( .26f, -.095f, -.0665f ),
			 new( -.26f, .095f, -.0665f ), new( -.26f, -.095f, -.0665f )], .0285f );
		for ( var i = 0; i < ArticulatedBoard.DynamicCount; ++i )
			board.Body( i ).Acceleration = Vector3.Zero;
		return board;
	}

	private static uint[] Record( DynamicBody body )
	{
		var record = new uint[ 64 ];
		void Vector( int offset, Vector3 value )
		{
			record[ offset ] = Word( value.X );
			record[ offset + 1 ] = Word( value.Y );
			record[ offset + 2 ] = Word( value.Z );
		}
		Vector( 0, body.Position );
		Vector( 4, body.Position );
		Vector( 8, Vector3.UnitZ );
		Vector( 24, body.Position );
		var inertia = body.WorldInverseInertia;
		Vector( 32, new( inertia.M11, inertia.M12, inertia.M13 ) );
		Vector( 40, new( inertia.M33, inertia.M22, inertia.M23 ) );
		record[ 35 ] = Word( body.InverseMass );
		record[ 43 ] = 4;
		record[ 47 ] = 1;
		record[ 3 ] = 1;
		record[ 7 ] = 8;
		return record;
	}

	private static uint Word( float value ) => BitConverter.SingleToUInt32Bits( value );
}
