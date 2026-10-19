using SkateMotion.Core.Grinding;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Clips reachable edges, ranks their endpoints and places a contact on the connected chain.</summary>
internal sealed partial class PhysicalHandSourceSelection
{
	private const float MinimumSquaredLength = .0000152587890625f;
	private readonly LimbSignedAngle _angles;
	private readonly bool[] _visited = new bool[ 40 ];
	internal RailSegment[] Segments { get; } = new RailSegment[ 40 ];
	internal bool[] Active { get; } = new bool[ 40 ];

	internal PhysicalHandSourceSelection( LimbSignedAngle angles )
	{
		ArgumentNullException.ThrowIfNull( angles );
		_angles = angles;
	}

	internal int Select( GrindEdgeRecord[] edges, int count, Vector3 firstLower, Vector3 secondLower,
		Vector3 firstUpper, Vector3 secondUpper, float depth, ref Vector3 point )
	{
		ArgumentNullException.ThrowIfNull( edges );
		if ( count < 0 || count > edges.Length || count > Segments.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		if ( !(secondUpper.X > firstUpper.X) )
		{
			(firstLower, secondLower) = (secondLower, firstLower);
			(firstUpper, secondUpper) = (secondUpper, firstUpper);
		}
		for ( var i = 0; i < count; i++ )
		{
			Segments[ i ] = edges[ i ].Segment;
			Active[ i ] = true;
			ClipDepth( i, -depth, false );
			ClipDepth( i, 0f, true );
			ClipSide( i, firstUpper, secondUpper );
			ClipSide( i, secondUpper, secondLower );
			ClipSide( i, secondLower, firstLower );
			ClipSide( i, firstLower, firstUpper );
		}

		var selected = HighestEdge( count, out var height );
		if ( selected < 0 )
			return selected;
		Array.Clear( _visited );
		_visited[ selected ] = true;
		var edge = Segments[ selected ];
		FollowChain( edge.Start, edge.End, count );
		FollowChain( edge.End, edge.Start, count );
		for ( var i = 0; i < count; i++ )
			Active[ i ] = _visited[ i ];

		var lower = Scale( firstLower, .5f ) + Scale( secondLower, .5f );
		var upper = Scale( firstUpper, .5f ) + Scale( secondUpper, .5f );
		var delta = upper - lower;
		var fraction = MultiplyAdd( Reciprocal( delta.Y ), height - lower.Y, 0f );
		fraction = fraction < 1f ? fraction : 1f;
		fraction = fraction > 0f ? fraction : 0f;
		var target = lower + Scale( delta, fraction );
		target.Z = 0f;
		point = ClosestPoint( target, count );
		// The selected source edge remains the height winner even when the contact lies on a neighbour.
		return selected;
	}

	private int HighestEdge( int count, out float height )
	{
		height = -float.MaxValue;
		var depth = -float.MaxValue;
		var selected = -1;
		for ( var i = 0; i < count; i++ )
		{
			if ( !Active[ i ] )
				continue;
			var edge = Segments[ i ];
			var endpoint = edge.Start.Y > edge.End.Y ? edge.Start : edge.End;
			if ( endpoint.Y > height + .1f || (endpoint.Y > height - .1f && endpoint.Z > depth) )
			{
				selected = i;
				height = endpoint.Y;
				depth = endpoint.Z;
			}
		}
		return selected;
	}

	private Vector3 ClosestPoint( Vector3 target, int count )
	{
		var point = Vector3.Zero;
		var nearest = float.MaxValue;
		for ( var i = 0; i < count; i++ )
		{
			if ( !Active[ i ] )
				continue;
			var edge = Segments[ i ];
			var delta = edge.End - edge.Start;
			var squared = Dot( delta, delta );
			var candidate = edge.Start;
			if ( squared > MinimumSquaredLength )
			{
				var fraction = MultiplyAdd( Reciprocal( squared ), Dot( target - edge.Start, delta ), 0f );
				if ( !(0f > fraction) )
					candidate = fraction > 1f ? edge.End : AddScaled( delta, fraction, edge.Start );
			}
			var separation = target - candidate;
			var distance = Dot( separation, separation );
			if ( nearest > distance )
			{
				nearest = distance;
				point = candidate;
			}
		}
		return point;
	}

	private void FollowChain( Vector3 first, Vector3 second, int count )
	{
		var index = 0;
		while ( index < count )
		{
			var current = index++;
			if ( !Active[ current ] || _visited[ current ] )
				continue;
			var edge = Segments[ current ];
			var separation = second - edge.Start;
			var direction = second - first;
			if ( .0024999999441206455f > Dot( separation, separation ) )
			{
				if ( !Aligned( direction, edge.End - edge.Start ) )
					continue;
				first = edge.Start;
				second = edge.End;
			}
			else
			{
				separation = second - edge.End;
				if ( !(.0024999999441206455f > Dot( separation, separation ))
					|| !Aligned( direction, edge.Start - edge.End ) )
					continue;
				first = edge.End;
				second = edge.Start;
			}
			_visited[ current ] = true;
			index = 0;
		}
	}

	private bool Aligned( Vector3 first, Vector3 second ) =>
		MathF.Abs( _angles.Wrapped( first, second, Vector3.Zero ) ) < .5235987901687622f;
}
