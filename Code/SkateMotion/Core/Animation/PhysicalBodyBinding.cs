namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using static LimbSolverMath;

/// <summary>Reads and writes a target frame through an optional physical body and local offset.</summary>
internal sealed class PhysicalBodyBinding
{
	internal LimbRigidTransform CachedPose { get; set; }
	internal LimbRigidTransform? LocalOffset { get; set; }
	internal PhysicalBodyState Body { get; set; }

	internal LimbRigidTransform Read()
	{
		if ( Body is null )
			return CachedPose;
		return LocalOffset is { } offset ? Body.Pose.Compose( offset ) : Body.Pose;
	}

	internal void Write( LimbRigidTransform pose )
	{
		if ( Body is not null )
		{
			var physical = LocalOffset is { } offset ? pose.Compose( offset.RigidInverse() ) : pose;
			WriteBody( Body, physical );
		}

		// Cache the requested frame, retaining its scale and any local offset.
		CachedPose = pose;
	}

	internal static void WriteBody( PhysicalBodyState body, LimbRigidTransform physical )
	{
		var rotation = NormalizeFrame( physical.Rotation );
		body.Pose = new LimbRigidTransform( rotation, physical.Translation );
		var q = PhysicalPoseOutput.Rotation( rotation );
		var squared = (MultiplyAdd( q.X, q.X, 0f ) + MultiplyAdd( q.Z, q.Z, 0f ))
			+ (MultiplyAdd( q.Y, q.Y, 0f ) + MultiplyAdd( q.W, q.W, 0f ));
		var inverse = InverseSquareRoot( squared );
		body.Orientation = new Vector4( MultiplyAdd( q.X, inverse, 0f ), MultiplyAdd( q.Y, inverse, 0f ),
			MultiplyAdd( q.Z, inverse, 0f ), MultiplyAdd( q.W, inverse, 0f ) );
		if ( body.Mass is { } mass )
			UpdateInertia( body, rotation, mass );
	}

	internal static void UpdateInertia( PhysicalBodyState body, LimbFrame frame, PhysicalBodyMass mass )
	{
		var x = Scale( frame.X, mass.InverseInertia.X );
		var y = Scale( frame.Y, mass.InverseInertia.Y );
		var z = Scale( frame.Z, mass.InverseInertia.Z );
		var first = AddScaled( z, frame.Z.X, AddScaled( x, frame.X.X, Scale( y, frame.Y.X ) ) );
		var zz = MultiplyAdd( z.Z, frame.Z.Z,
			MultiplyAdd( x.Z, frame.X.Z, MultiplyAdd( y.Z, frame.Y.Z, 0f ) ) );
		var yy = MultiplyAdd( z.Y, frame.Z.Y,
			MultiplyAdd( x.Y, frame.X.Y, MultiplyAdd( y.Y, frame.Y.Y, 0f ) ) );
		var yz = MultiplyAdd( z.Z, frame.Z.Y,
			MultiplyAdd( x.Z, frame.X.Y, MultiplyAdd( y.Z, frame.Y.Y, 0f ) ) );
		body.InverseInertiaDiagonal = new Vector3( first.X, yy, zz );
		body.InverseInertiaOffDiagonal = new Vector3( first.Y, first.Z, yz );
		body.InverseMass = mass.InverseMass;
	}

	private static LimbFrame NormalizeFrame( LimbFrame frame )
	{
		var x = Normalize( frame.X );
		var y = Normalize( frame.Y );
		var z = Normalize( frame.Z );
		var missing = !(Dot( frame.X, frame.X ) > 0f) ? 0
			: !(Dot( frame.Y, frame.Y ) > 0f) ? 1
			: !(Dot( frame.Z, frame.Z ) > 0f) ? 2 : -1;
		var yz = MathF.Abs( Dot( y, z ) );
		var zx = MathF.Abs( Dot( z, x ) );
		var xy = MathF.Abs( Dot( x, y ) );

		// Preserve the least-aligned pair. Strict comparisons retain the diagonal tie order.
		if ( missing == 0 || (missing == -1 && zx > yz && xy > yz) )
		{
			x = Normalize( Cross( y, z ) );
			z = Normalize( Cross( x, y ) );
		}
		else if ( missing == 1 || (missing == -1 && zx <= yz && xy > zx) )
		{
			y = Normalize( Cross( z, x ) );
			x = Normalize( Cross( y, z ) );
		}
		else
		{
			z = Normalize( Cross( x, y ) );
			y = Normalize( Cross( z, x ) );
		}

		return new LimbFrame( x, y, z );
	}
}
