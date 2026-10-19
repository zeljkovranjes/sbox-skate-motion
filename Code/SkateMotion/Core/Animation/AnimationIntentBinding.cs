using SkateMotion.Core.Input;

namespace SkateMotion.Core.Animation;

/// <summary>Connects stateless intent behaviors to the current request table and shared scalar outputs.</summary>
internal sealed class AnimationIntentBinding
{
	private readonly InputRequestBuffer _requests;
	private readonly AnimationScalarOutput _output;

	internal AnimationIntentBinding( InputRequestBuffer requests, AnimationScalarOutput output )
	{
		_requests = requests ?? throw new ArgumentNullException( nameof( requests ) );
		_output = output ?? throw new ArgumentNullException( nameof( output ) );
	}

	internal CompiledStateBehaviorBinding Bind() => new( () => -1, _ => { }, _ => { }, () => { } );

	internal void Update( AnimationIntentDefinition definition )
	{
		for ( var index = 0; index < _requests.Count; index++ )
		{
			var request = _requests[ index ];
			if ( request.Key != definition.Request )
				continue;
			_output.Enqueue( definition.Target, request.Value );
			if ( definition.PublishAnimation )
				_output.Attributes.WriteScalar( definition.Target, request.Value, false, -1 );
			return;
		}
	}
}
