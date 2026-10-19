namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Constructs a board-pivot attachment correction from accumulated approach offsets.</summary>
internal sealed class GrindAttachmentFrameBuilder
{
	private readonly LimbSolverMath _math;

	internal GrindAttachmentFrameBuilder( LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		_math = math;
	}

	internal LimbRigidTransform Build( GrindAttachmentInput input, GrindApproachState state )
	{
		ArgumentNullException.ThrowIfNull( state );
		var edge = state.EdgeFrame.Z;
		var side = Normalize( Cross( Vector3.UnitY, edge ) );
		var up = Normalize( Cross( edge, side ) );
		var angles = state.AngularPosition;
		var rotation = Rotate( Rotation( input.AuthoredBoard.Rotation.Z, angles.Z ), input.ModelToWorld.Rotation );
		rotation = Rotate( Rotation( edge, angles.X ), rotation );
		rotation = Rotate( Rotation( up, angles.Y ), rotation );
		rotation = Rotate( input.WorldToModel.Rotation, rotation );
		// Composed frame translation is discarded before pivoting about the mutable board.
		var pivot = input.MutableBoard.Translation;
		var translation = rotation.Transform( -pivot, Vector3.Zero );
		var offset = input.WorldToModel.Rotation.Transform( state.CorrectionPosition, Vector3.Zero ) + pivot;
		var identity = LimbRigidTransform.Identity.Rotation;
		var axes = Rotate( identity, Rotate( rotation, identity ) );
		return new LimbRigidTransform( axes, identity.Transform( translation, offset ) );
	}

	private LimbFrame Rotation( Vector3 axis, float angle )
	{
		var (sine, cosine) = _math.SinCos( angle );
		var s = Scale( axis, sine );
		var t = Scale( axis, 1f - cosine );
		// This path rounds each product before adding its diagonal or off-axis term.
		return new LimbFrame(
			new Vector3( MultiplyAdd( t.X, axis.X, 0f ) + cosine, MultiplyAdd( t.X, axis.Y, 0f ) + s.Z, MultiplyAdd( t.X, axis.Z, 0f ) - s.Y ),
			new Vector3( MultiplyAdd( t.Y, axis.X, 0f ) - s.Z, MultiplyAdd( t.Y, axis.Y, 0f ) + cosine, MultiplyAdd( t.Y, axis.Z, 0f ) + s.X ),
			new Vector3( MultiplyAdd( t.Z, axis.X, 0f ) + s.Y, MultiplyAdd( t.Z, axis.Y, 0f ) - s.X, MultiplyAdd( t.Z, axis.Z, 0f ) + cosine ) );
	}

	private static LimbFrame Rotate( LimbFrame left, LimbFrame right ) => new(
		left.Transform( right.X, Vector3.Zero ),
		left.Transform( right.Y, Vector3.Zero ),
		left.Transform( right.Z, Vector3.Zero ) );
}
