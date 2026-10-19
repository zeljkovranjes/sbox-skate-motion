using SkateMotion.Core.Constraints;

namespace SkateMotion.Core.Collision;

using Vector3 = System.Numerics.Vector3;

/// <summary>Retains seven owner snapshots and their primitive geometry for map contact generation.</summary>
internal sealed class BoardCollisionInputs
{
	private readonly CollisionMapBodyInput[] _bodies = new CollisionMapBodyInput[ 7 ];
	private readonly uint[][] _transforms = new uint[ 7 ][];
	private readonly uint[][] _bounds = new uint[ 7 ][];
	private readonly uint[][] _states = new uint[ 7 ][];

	internal ReadOnlyMemory<CollisionMapBodyInput> Bodies => _bodies;

	internal BoardCollisionInputs( Vector3 deckSize, Vector3 truckSize, float wheelRadius,
		float wheelWidth, uint firstShapeIdentity = 1 )
	{
		if ( !Positive( deckSize ) || !Positive( truckSize ) || !float.IsFinite( wheelRadius )
			|| !float.IsFinite( wheelWidth ) || wheelRadius <= 0f || wheelWidth <= 0f )
			throw new ArgumentException( "Board collision dimensions must be finite and positive." );
		if ( firstShapeIdentity > uint.MaxValue - 6 )
			throw new ArgumentOutOfRangeException( nameof( firstShapeIdentity ) );
		for ( var i = 0; i < _bodies.Length; ++i )
		{
			var shape = new uint[ 24 ];
			shape[ 0 ] = Bits( 1f );
			shape[ 5 ] = Bits( 1f );
			shape[ 10 ] = Bits( 1f );
			shape[ 15 ] = Bits( 1f );
			shape[ 23 ] = 1;
			var kind = i < 3 ? 4 : 5;
			if ( kind == 4 )
			{
				var half = (i == 0 ? deckSize : truckSize) * .5f;
				shape[ 17 ] = Bits( half.X );
				shape[ 18 ] = Bits( half.Y );
				shape[ 19 ] = Bits( half.Z );
			}
			else
			{
				// The owner's axle is local Y; the primitive's longitudinal axis is its third row.
				shape[ 5 ] = 0;
				shape[ 6 ] = Bits( -1f );
				shape[ 9 ] = Bits( 1f );
				shape[ 10 ] = 0;
				shape[ 17 ] = Bits( wheelWidth * .5f );
				shape[ 18 ] = Bits( wheelRadius );
			}
			_transforms[ i ] = new uint[ 16 ];
			_bounds[ i ] = new uint[ 8 ];
			_states[ i ] = new uint[ 44 ];
			_bodies[ i ] = new CollisionMapBodyInput
			{
				Shape = shape,
				ShapeKind = kind,
				ObjectType = (uint)kind,
				ShapeIdentity = firstShapeIdentity + (uint)i,
				BodyIdentity = (uint)i + 1,
				BaseMargin = 0x3d4ccccdu,
				Transform = _transforms[ i ],
				Bounds = _bounds[ i ],
				BodyState = _states[ i ]
			};
		}
	}

	/// <summary>Creates spherical wheels and longitudinal capsule trucks with explicitly supplied dimensions.</summary>
	internal static BoardCollisionInputs CreateRoundParts( Vector3 deckSize,
		float truckRadius, float truckHalfLength, float wheelRadius, uint firstShapeIdentity = 1 )
	{
		if ( !float.IsFinite( truckRadius ) || !float.IsFinite( truckHalfLength )
			|| truckRadius <= 0f || truckHalfLength <= 0f )
			throw new ArgumentException( "Truck capsule dimensions must be finite and positive." );
		var result = new BoardCollisionInputs( deckSize,
			new Vector3( truckRadius * 2f, truckRadius * 2f, truckHalfLength * 2f ),
			wheelRadius, wheelRadius * 2f, firstShapeIdentity );
		for ( var i = 1; i < result._bodies.Length; ++i )
		{
			var shape = result._bodies[ i ].Shape.Span;
			shape.Clear();
			shape[ 0 ] = shape[ 5 ] = shape[ 10 ] = Bits( 1f );
			shape[ 23 ] = 1;
			var kind = i < 3 ? 2 : 1;
			shape[ 20 ] = Bits( i < 3 ? truckRadius : wheelRadius );
			if ( kind == 2 )
			{
				// Capsule length follows the board's forward axis, independently of its wheel axle.
				shape[ 0 ] = shape[ 10 ] = 0;
				shape[ 2 ] = Bits( -1f );
				shape[ 8 ] = Bits( 1f );
				shape[ 17 ] = Bits( truckHalfLength );
			}
			result._bodies[ i ] = result._bodies[ i ] with { ShapeKind = kind, ObjectType = (uint)kind };
		}
		return result;
	}

	internal void Update( ReadOnlySpan<DynamicBody> bodies, uint category, uint material,
		float staticFriction, float slidingFriction, float restitution, bool active = true )
	{
		if ( bodies.Length < _bodies.Length )
			throw new ArgumentException( "All seven board bodies are required.", nameof( bodies ) );
		if ( !float.IsFinite( staticFriction ) || !float.IsFinite( slidingFriction ) || !float.IsFinite( restitution )
			|| staticFriction < 0f || slidingFriction < 0f || restitution < 0f )
			throw new ArgumentException( "Contact material coefficients must be finite and nonnegative." );
		for ( var i = 0; i < _bodies.Length; ++i )
		{
			_bodies[ i ].Shape.Span[ 22 ] = material;
			CollisionBodyCache.Write( bodies[ i ], 4, _states[ i ] );
			CollisionBodyCache.WriteTransform( bodies[ i ], _transforms[ i ] );
			CollisionBodyBounds.Write( _bodies[ i ].ShapeKind, _bodies[ i ].Shape.Span,
				_transforms[ i ], _states[ i ], _bounds[ i ] );
			_bodies[ i ] = _bodies[ i ] with
			{
				Category = category,
				Material = material,
				StaticFriction = Bits( staticFriction ),
				SlidingFriction = Bits( slidingFriction ),
				Restitution = Bits( restitution ),
				Active = active,
				Disabled = false
			};
		}
	}

	private static uint Bits( float value ) => BitConverter.SingleToUInt32Bits( value );

	private static bool Positive( Vector3 value ) => float.IsFinite( value.X ) && float.IsFinite( value.Y )
		&& float.IsFinite( value.Z ) && value.X > 0f && value.Y > 0f && value.Z > 0f;
}
