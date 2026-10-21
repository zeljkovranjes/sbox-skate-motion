namespace SkateMotion.Engine.Collision;

using Sandbox;
using System.Collections.Generic;

/// <summary>
/// One board's read collision shapes, shared by its wheel sweeps and its contact generation. A shape's triangles are
/// read once and kept until the shape is gone: reading allocates in the engine, and a level's world mesh is large, so
/// reading it again for every reset or for each user of it stalled the game for a moment each time.
/// </summary>
internal sealed class MapShapeCache
{
	private readonly Dictionary<PhysicsShape, MapCollisionShape> _shapes = new();
	private readonly List<PhysicsShape> _gone = new();
	private uint _nextIdentity;

	/// <summary>Shapes whose triangles have been read so far.</summary>
	internal int GeometryBuilds { get; private set; }

	/// <summary>The shape's triangles, read now if they have not been yet.</summary>
	internal MapCollisionShape Get( PhysicsShape shape, Func<PhysicsShape, int, ushort> material )
	{
		if ( !_shapes.TryGetValue( shape, out var entry ) )
		{
			entry = new( shape, checked( ++_nextIdentity ) );
			_shapes.Add( shape, entry );
		}
		if ( !entry.Read && entry.Refresh( material ) )
			GeometryBuilds++;
		return entry;
	}

	/// <summary>The shape's entry if it has been read.</summary>
	internal bool TryGet( PhysicsShape shape, out MapCollisionShape entry ) => _shapes.TryGetValue( shape, out entry );

	/// <summary>Forgets shapes that no longer exist.</summary>
	internal void Prune()
	{
		_gone.Clear();
		foreach ( var shape in _shapes.Keys )
		{
			if ( !shape.IsValid() )
				_gone.Add( shape );
		}
		foreach ( var shape in _gone )
			_shapes.Remove( shape );
	}
}
