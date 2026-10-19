using SkateMotion.Core.Constraints;
using static SkateMotion.Core.Collision.CollisionArithmetic;

namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

internal static partial class CollisionMapContactBatch
{
	private static void WriteContact( CollisionMapBodyInput body, CollisionMapContactContext context,
		uint triangleMaterial, ReadOnlySpan<uint> manifold, int point, Span<uint> output )
	{
		var firstState = body.BodyState.Span;
		var secondState = context.BodyState.Span;
		var firstPoint = manifold.Slice( 12 + point * 8, 4 );
		var secondPoint = manifold.Slice( 16 + point * 8, 4 );
		var normal = manifold.Slice( 8, 4 );
		var staticFriction = Greater( context.StaticFriction, body.StaticFriction ) ? context.StaticFriction : body.StaticFriction;
		var slidingFriction = Greater( context.SlidingFriction, body.SlidingFriction ) ? context.SlidingFriction : body.SlidingFriction;
		var restitution = Greater( body.Restitution, context.Restitution ) ? context.Restitution : body.Restitution;
		var contact = new ContactManifoldPoint( 0, 0, Vector( firstPoint ), Vector( secondPoint ), Vector( normal ),
			Value( staticFriction ), Value( slidingFriction ), Value( restitution ) );
		var geometry = GeneratedContactPreparation.Prepare( Dynamic( firstState ), Dynamic( secondState ), contact );
		firstPoint.CopyTo( output );
		secondPoint.CopyTo( output[ 4.. ] );
		normal.CopyTo( output[ 8.. ] );
		Write( geometry.TangentA, output[ 12.. ] );
		Write( geometry.TangentB, output[ 16.. ] );
		Write( geometry.MotionTerm, output[ 20.. ] );
		output[ 3 ] = body.BodyIdentity;
		output[ 7 ] = context.BodyIdentity;
		output[ 11 ] = restitution;
		output[ 15 ] = staticFriction;
		output[ 19 ] = slidingFriction;
		output[ 23 ] = (body.Material << 16) | (triangleMaterial >> 16);
		firstState.Slice( 4, 4 ).CopyTo( output[ 24.. ] );
		secondState.Slice( 4, 4 ).CopyTo( output[ 28.. ] );
		for ( var block = 0; block < 4; ++block )
		{
			firstState.Slice( 28 + block * 4, 4 ).CopyTo( output[ (32 + block * 8).. ] );
			secondState.Slice( 28 + block * 4, 4 ).CopyTo( output[ (36 + block * 8).. ] );
		}
	}

	private static DynamicBody Dynamic( ReadOnlySpan<uint> state ) => new()
	{
		Position = Vector( state[ 4.. ] ),
		Velocity = Vector( state[ 8.. ] ),
		AngularVelocity = Vector( state[ 12.. ] )
	};

	private static Vector3 Vector( ReadOnlySpan<uint> words ) => new( Value( words[ 0 ] ), Value( words[ 1 ] ), Value( words[ 2 ] ) );

	private static void Write( Vector3 vector, Span<uint> words )
	{
		words[ 0 ] = Word( vector.X );
		words[ 1 ] = Word( vector.Y );
		words[ 2 ] = Word( vector.Z );
	}
}
