namespace SkateMotion.Engine.Collision;

using Sandbox;
using System.Collections.Generic;
using SkateMotion.Core.Collision;

/// <summary>Retains generated map contacts and identifies shapes already represented by that stream.</summary>
internal sealed class MapContactStream
{
	private readonly MapCollisionSource _source;
	private readonly CollisionContactBuffer _output = new();
	private readonly uint[] _state = new uint[ 0x3290 / 4 ];
	private readonly CollisionContactCollector _collector;
	private readonly HashSet<PhysicsShape> _represented = new();
	private readonly Func<PhysicsShape, CollisionMapContactContext?> _binding;
	private readonly Func<PhysicsShape, CollisionMapContactContext?> _trackedBinding;
	private readonly uint _duplicateSquaredTolerance;
	private readonly bool _deferredReduction;

	internal ReadOnlySpan<uint> Records => _output.Records;
	internal int Count => _output.Count;
	internal int TriangleCount { get; private set; }

	internal MapContactStream( MapCollisionSource source,
		Func<PhysicsShape, CollisionMapContactContext?> binding,
		uint duplicateSquaredTolerance, bool deferredReduction )
	{
		ArgumentNullException.ThrowIfNull( source );
		ArgumentNullException.ThrowIfNull( binding );
		_source = source;
		_binding = binding;
		_trackedBinding = Bind;
		_duplicateSquaredTolerance = duplicateSquaredTolerance;
		_deferredReduction = deferredReduction;
		_collector = new( _state, _output );
	}

	internal bool ContainsShape( PhysicsShape shape ) => _represented.Contains( shape );

	internal void Reset()
	{
		_output.Reset();
		_represented.Clear();
		TriangleCount = 0;
		_state[ 0 ] = 0;
		_state[ 1 ] = 0;
		// Synchronous output grows as batches flush; there is no fixed external DMA reservation.
		_state[ 2 ] = uint.MaxValue;
		_state[ 3 ] = 0;
		_state[ 4 ] = _duplicateSquaredTolerance;
		_state[ 0x328c / 4 ] = 0x01000000u | (_deferredReduction ? 0x00010000u : 0u);
	}

	/// <param name="nearMinimum">Lowest corner (world metres) of the box the board's bodies can reach this step.</param>
	/// <param name="nearMaximum">Highest corner of that box.</param>
	/// <param name="skip">Shapes the board does not collide with this step, or null.</param>
	internal void Generate( ReadOnlyMemory<CollisionMapBodyInput> bodies, System.Numerics.Vector3 nearMinimum,
		System.Numerics.Vector3 nearMaximum, Func<PhysicsShape, bool> skip = null )
	{
		Reset();
		TriangleCount = _source.GenerateContacts( bodies, nearMinimum, nearMaximum, _trackedBinding, _collector, skip );
		_collector.Flush();
	}

	private CollisionMapContactContext? Bind( PhysicsShape shape )
	{
		var context = _binding( shape );
		if ( context.HasValue ) _represented.Add( shape );
		return context;
	}
}
