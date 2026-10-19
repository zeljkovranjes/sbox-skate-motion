namespace SkateMotion.Core.Animation;

/// <summary>Publishes graph scalar assignments in phase order to shared physical and animation outputs.</summary>
internal sealed class AnimationScalarBehaviorBinding
{
	private readonly AnimationScalarOutput _output;

	internal AnimationScalarBehaviorBinding( AnimationScalarOutput output ) =>
		_output = output ?? throw new ArgumentNullException( nameof( output ) );

	internal CompiledStateBehaviorBinding Bind( AnimationScalarBehaviorDefinition definition ) => new(
		() => -1,
		_ => Publish( definition, definition.EnterValue ),
		_ => Publish( definition, definition.ExitValue ),
		() => Publish( definition, definition.EnterValue ) );

	internal void Update( AnimationScalarBehaviorDefinition definition ) =>
		Publish( definition, definition.UpdateValue );

	private void Publish( AnimationScalarBehaviorDefinition definition, float? value )
	{
		if ( !value.HasValue )
			return;
		_output.Enqueue( definition.Name, value.Value );
		if ( definition.PublishAnimation )
			_output.Attributes.WriteScalar( definition.Name, value.Value, false, -1 );
	}
}
