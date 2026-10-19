namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Runs static source lookup, local reach prediction and selection into a retained world contact.</summary>
internal sealed class PhysicalHandSourceController
{
	private readonly PhysicalHandReachBuilder _reach;
	private readonly PhysicalHandSourceSelection _selection;
	private readonly float _depth;
	internal PhysicalHandSourceLookup Lookup { get; } = new();

	internal PhysicalHandSourceController( PhysicalHandReachBuilder reach, PhysicalHandSourceSelection selection, float depth )
	{
		ArgumentNullException.ThrowIfNull( reach );
		ArgumentNullException.ThrowIfNull( selection );
		_reach = reach;
		_selection = selection;
		_depth = depth;
	}

	internal void Update( GrindEdgeChunk[] chunks, PhysicalHandSourceInput input, PhysicalHandSourceState state )
	{
		ArgumentNullException.ThrowIfNull( state );
		// A source lookup miss preserves output; reach and selection failures only clear activation.
		if ( !Lookup.TryPrepare( chunks, input.Origin, input.Normal ) )
			return;
		var geometry = Lookup.Geometry;
		var origin = geometry.Inverse.Transform( input.Origin );
		var velocity = geometry.Inverse.Rotation.Transform( input.Velocity, Vector3.Zero );
		var motion = PhysicalHandReachBuilder.ShapeMotion( velocity );
		var side = motion.X > 0f ? 1 : 2;
		if ( input.RequestedSide != 0 && input.RequestedSide != side )
			motion.X = input.RequestedSide == 1 ? .0000152587890625f : -.0000152587890625f;
		var region = _reach.BuildShaped( origin, motion );
		state.Enabled = false;
		if ( !region.IsValid )
			return;
		var localPoint = Vector3.Zero;
		var selected = _selection.Select( geometry.LocalEdges, Lookup.Count,
			region.FirstLower, region.SecondLower, region.FirstUpper, region.SecondUpper, _depth, ref localPoint );
		if ( selected < 0 )
			return;
		state.Enabled = true;
		state.Point = geometry.Frame.Transform( localPoint );
		state.Side = motion.X > 0f ? 1 : 2;
		var edge = geometry.WorldEdges[ selected ];
		state.Edge = edge with { Flags = (state.Edge.Flags & 0x7fffffffu) | (edge.Flags & 0x80000000u) };
	}
}
