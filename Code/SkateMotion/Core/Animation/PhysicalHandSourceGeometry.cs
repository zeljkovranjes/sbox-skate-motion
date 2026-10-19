namespace SkateMotion.Core.Animation;

using SkateMotion.Core.Grinding;
using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Builds a surface frame and paired world/local edge candidates with retained metadata.</summary>
internal sealed class PhysicalHandSourceGeometry
{
	internal GrindEdgeRecord[] WorldEdges { get; } = new GrindEdgeRecord[ 40 ];
	internal GrindEdgeRecord[] LocalEdges { get; } = new GrindEdgeRecord[ 40 ];
	internal int Count { get; private set; }
	internal LimbRigidTransform Frame { get; private set; }
	internal LimbRigidTransform Inverse { get; private set; }

	internal void Prepare( Vector3 origin, Vector3 normal, GrindEdgeRecord[] edges, int count )
	{
		ArgumentNullException.ThrowIfNull( edges );
		if ( count < 0 || count > edges.Length )
			throw new ArgumentOutOfRangeException( nameof( count ) );
		Frame = BuildFrame( origin, normal );
		Inverse = Frame.RigidInverse();
		Count = 0;
		for ( var i = 0; i < count; i++ )
		{
			var edge = edges[ i ];
			var first = Inverse.Transform( edge.Segment.Start );
			var second = Inverse.Transform( edge.Segment.End );
			var delta = second - first;
			var length = SquareRoot( Dot( delta, delta ) );
			var direction = length > .000001f ? Normalize( delta ) : Vector3.Zero;
			if ( !(MathF.Abs( direction.X ) > .7099999785423279f) || Count >= WorldEdges.Length )
				continue;
			WorldEdges[ Count ] = edge with
			{
				Flags = CopyFlag( WorldEdges[ Count ].Flags, edge.Flags )
			};
			LocalEdges[ Count ] = new GrindEdgeRecord( new RailSegment( first, second ), edge.Metadata,
				CopyFlag( LocalEdges[ Count ].Flags, edge.Flags ) );
			Count++;
		}
	}

	internal static LimbRigidTransform BuildFrame( Vector3 origin, Vector3 normal )
	{
		if ( MathF.Abs( normal.Y ) > .9900000095367432f )
			return LimbRigidTransform.Identity with { Translation = origin };
		var x = Normalize( Cross( Vector3.UnitY, normal ) );
		return new LimbRigidTransform( new LimbFrame( x, Vector3.UnitY, Cross( x, Vector3.UnitY ) ), origin );
	}

	private static uint CopyFlag( uint retained, uint source ) =>
		(retained & 0x7fffffffu) | (source & 0x80000000u);
}
