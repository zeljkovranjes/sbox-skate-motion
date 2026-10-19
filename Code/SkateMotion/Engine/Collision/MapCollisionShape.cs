namespace SkateMotion.Engine.Collision;

using Sandbox;
using SkateMotion.Core.Collision;
using NumericsVector = System.Numerics.Vector3;

/// <summary>Caches a shape's local triangles independently of its body's moving frame.</summary>
internal sealed class MapCollisionShape
{
	private Vector3[] _positions = [];
	private uint[] _indices = [];
	internal PhysicsShape Shape { get; }
	internal uint Identity { get; }
	internal bool Seen { get; set; }
	internal int Filter { get; set; } = -1;
	internal uint Exclusion { get; set; }
	internal MeshQueryGeometry Geometry { get; private set; } = new( [], NumericsVector.Zero, NumericsVector.Zero );

	internal MapCollisionShape( PhysicsShape shape, uint identity )
	{
		Shape = shape;
		Identity = identity;
	}

	internal bool Refresh( Func<PhysicsShape, int, ushort> material )
	{
		Shape.Triangulate( out var positions, out var indices );
		if ( indices.Length % 3 != 0 )
			throw new InvalidOperationException( "Collision shape returned incomplete triangles." );
		foreach ( var point in positions )
		{
			if ( !float.IsFinite( point.x ) || !float.IsFinite( point.y ) || !float.IsFinite( point.z ) )
				throw new InvalidOperationException( "Collision shape returned a nonfinite vertex." );
		}
		foreach ( var index in indices )
		{
			if ( index >= positions.Length )
				throw new InvalidOperationException( "Collision shape returned an invalid vertex index." );
		}

		var changed = positions.Length != _positions.Length || indices.Length != _indices.Length;
		for ( var i = 0; !changed && i < positions.Length; i++ )
			changed = positions[ i ] != _positions[ i ];
		for ( var i = 0; !changed && i < indices.Length; i++ )
			changed = indices[ i ] != _indices[ i ];
		for ( var i = 0; !changed && i < Geometry.Triangles.Length; i++ )
			changed = Geometry.Triangles[ i ].Material != material( Shape, i );
		if ( !changed )
			return false;

		var triangles = new MeshTriangle[ indices.Length / 3 ];
		var minimum = new NumericsVector( float.MaxValue );
		var maximum = new NumericsVector( -float.MaxValue );
		foreach ( var point in positions )
		{
			var converted = MapCollisionCoordinates.Point( point );
			minimum = NumericsVector.Min( minimum, converted );
			maximum = NumericsVector.Max( maximum, converted );
		}
		for ( var i = 0; i < triangles.Length; i++ )
		{
			triangles[ i ] = new( MapCollisionCoordinates.Point( positions[ indices[ i * 3 ] ] ),
				MapCollisionCoordinates.Point( positions[ indices[ i * 3 + 1 ] ] ),
				MapCollisionCoordinates.Point( positions[ indices[ i * 3 + 2 ] ] ), material( Shape, i ) );
		}
		_positions = positions;
		_indices = indices;
		Geometry = new( triangles, minimum, maximum );
		return true;
	}

	internal MeshQueryInstance Instance()
	{
		if ( !Shape.IsValid() || !Shape.Body.IsValid() || Shape.IsTrigger || !Shape.EnableSolidCollisions )
			return new( new( [], NumericsVector.Zero, NumericsVector.Zero ), default, default,
				NumericsVector.Zero, NumericsVector.Zero, Filter, Exclusion, Identity );
		var frame = Shape.Body.Transform;
		var inverseRotation = frame.Rotation.Inverse;
		return new( Geometry, MapCollisionCoordinates.Frame( frame.Position, frame.Rotation ),
			MapCollisionCoordinates.Frame( inverseRotation * -frame.Position, inverseRotation ),
			Geometry.Minimum, Geometry.Maximum, Filter, Exclusion, Identity );
	}
}
