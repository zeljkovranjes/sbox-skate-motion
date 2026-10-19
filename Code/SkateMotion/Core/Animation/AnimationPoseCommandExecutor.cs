namespace SkateMotion.Core.Animation;

/// <summary>Dispatches authored value, weight and combine commands with reusable weight storage.</summary>
internal sealed class AnimationPoseCommandExecutor
{
	private const float WeightScale = 1f / 1048575f;
	private readonly IAnimationPoseCommandOperations _operations;
	private readonly float[] _weights = new float[ AnimationPoseMultiBlender.MaximumInputs ];

	internal AnimationPoseCommandExecutor( IAnimationPoseCommandOperations operations )
	{
		ArgumentNullException.ThrowIfNull( operations );
		_operations = operations;
	}

	internal AnimationPoseCommandResult Execute( ReadOnlySpan<uint> commands, int sampleLimit )
	{
		uint depth = 0;
		var values = 0;
		var weightCount = 0;
		for ( var index = 0; index < commands.Length; index++ )
		{
			var word = commands[ index ];
			var opcode = word >> 30;
			if ( opcode == 0 )
			{
				_operations.Sample( (int)( ( word >> 23 ) & 127 ) );
				depth++;
				values++;
				continue;
			}
			if ( opcode == 1 )
			{
				if ( weightCount == _weights.Length )
					throw new InvalidOperationException( "A blend command exceeds the supported pose input count." );
				_weights[ weightCount++ ] = ( word & 0xfffff ) * WeightScale;
				continue;
			}

			if ( depth > 1 && weightCount == 1 )
			{
				_operations.Blend( _weights[ 0 ] );
				depth--;
			}
			else if ( weightCount > 1 )
			{
				_operations.BlendMany( _weights.AsSpan( 0, weightCount ) );
				depth = unchecked( depth - (uint)weightCount + 1 );
			}

			// Limits apply only after a combine, once its operand group has reduced.
			if ( values > sampleLimit && depth <= 1 )
				return new AnimationPoseCommandResult( depth, values, weightCount, index );
			weightCount = 0;
		}
		return new AnimationPoseCommandResult( depth, values, weightCount, commands.Length );
	}

	/// <summary>Executes a registered stream; the caller handles missing-stream fallback and pose-stack reset.</summary>
	internal bool TryExecute( AnimationPoseStreamContext context, AnimationPoseStreamKey name,
		int sampleLimit, out AnimationPoseCommandResult result )
	{
		ArgumentNullException.ThrowIfNull( context );
		var slot = context.Find( name );
		if ( slot < 0 || context.Commands( slot ).IsEmpty )
		{
			result = default;
			return false;
		}
		result = Execute( context.Commands( slot ), sampleLimit );
		return true;
	}
}
