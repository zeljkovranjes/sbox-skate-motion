namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Requests a blended board alignment during permitted negative vertical motion.</summary>
internal sealed class DescendingAttachmentController
{
	private readonly LimbPoseBlender _blender;
	private readonly float[] _inputs;
	private readonly float[] _weights;

	internal DescendingAttachmentController( LimbPoseBlender blender, float[] inputs, float[] weights )
	{
		ArgumentNullException.ThrowIfNull( blender );
		ArgumentNullException.ThrowIfNull( inputs );
		ArgumentNullException.ThrowIfNull( weights );
		if ( inputs.Length != 8 || weights.Length != 8 )
			throw new ArgumentException( "Descending attachment requires eight response points." );
		_blender = blender;
		_inputs = new float[ 8 ];
		_weights = new float[ 8 ];
		for ( var i = 0; i < 8; i++ )
		{
			if ( !float.IsFinite( inputs[ i ] ) || !float.IsFinite( weights[ i ] )
				|| (i > 0 && inputs[ i ] < inputs[ i - 1 ]) )
				throw new ArgumentException( "Descending response inputs must be ordered and all points finite." );
			_inputs[ i ] = inputs[ i ];
			_weights[ i ] = weights[ i ];
		}
	}

	internal void Update( DescendingAttachmentInput input, BoardAttachmentController attachment )
	{
		ArgumentNullException.ThrowIfNull( attachment );
		if ( !(input.VerticalMotion < 0f) || input.Blocked )
			return;

		var target = input.WorldToModel.Compose( input.TargetWorld );
		var offset = input.WorldToModel.Rotation.Transform( input.WorldDisplacement, Vector3.Zero );
		offset.Y = target.Translation.Y > input.Board.Translation.Y
			? target.Translation.Y - input.Board.Translation.Y : 0f;
		target = target with { Translation = input.Board.Translation + offset };
		var blended = _blender.BlendTransform( input.Board, target, Weight( input.ResponseParameter ) );
		var correction = blended.Compose( input.Board.RigidInverse() );
		var position = correction.Translation;
		attachment.RequestFrame( correction with { Translation = new Vector3( position.X, 0f, position.Z ) } );
		attachment.RequestHeight( MultiplyAdd( -1f, position.Y, 0f ), 15f );
	}

	private float Weight( float value )
	{
		if ( value < _inputs[ 0 ] )
			return _weights[ 0 ];
		if ( value >= _inputs[ 7 ] )
			return _weights[ 7 ];
		for ( var i = 1; i < 8; i++ )
		{
			if ( value >= _inputs[ i ] )
				continue;
			var width = _inputs[ i ] - _inputs[ i - 1 ];
			if ( width <= 0f )
				return _weights[ i ];
			var slope = (_weights[ i ] - _weights[ i - 1 ]) / width;
			return MathF.FusedMultiplyAdd( slope, value - _inputs[ i - 1 ], _weights[ i - 1 ] );
		}
		return _weights[ 0 ];
	}
}
