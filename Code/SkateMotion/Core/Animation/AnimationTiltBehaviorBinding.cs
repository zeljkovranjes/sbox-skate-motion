using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects retained tilt instances to shared actor flags and animation overrides.</summary>
internal sealed class AnimationTiltBehaviorBinding
{
	private readonly AnimationGameplayFlags _gameplay;
	private readonly AnimationPlayActorState _actor;
	private readonly Func<AnimationTiltObservation> _physicalOutput;
	private readonly Func<TiltParameterController> _createController;
	private readonly AnimationAttributeOverrideBuffer _attributes;
	private readonly List<TiltParameterController> _instances = new();
	private readonly List<int> _references = new();

	internal AnimationTiltBehaviorBinding( AnimationGameplayFlags gameplay, AnimationPlayActorState actor,
		Func<AnimationTiltObservation> physicalOutput, Func<TiltParameterController> createController,
		AnimationAttributeOverrideBuffer attributes )
	{
		ArgumentNullException.ThrowIfNull( gameplay );
		ArgumentNullException.ThrowIfNull( actor );
		ArgumentNullException.ThrowIfNull( physicalOutput );
		ArgumentNullException.ThrowIfNull( createController );
		ArgumentNullException.ThrowIfNull( attributes );
		_gameplay = gameplay;
		_actor = actor;
		_physicalOutput = physicalOutput;
		_createController = createController;
		_attributes = attributes;
	}

	internal CompiledStateBehaviorBinding Bind() => new( Create, _ => { }, _ => { }, () => { } );

	internal CompiledStateBehaviorBinding BindApplication() => _gameplay.Bind(
		AnimationGameplayFlag.BodyTilt, () => -1, () => _gameplay.Enter( AnimationGameplayFlag.BodyTilt ) );

	internal void Update( AnimationAttributeKey outputName, int instance )
	{
		var controller = Retained( instance );
		if ( !_gameplay.IsBodyTiltEnabled )
		{
			controller.Step( false, false, 0f, 0f, 0 );
			return;
		}
		var physical = _physicalOutput();
		var mirrored = (_actor.Flags & (1UL << 30)) != 0;
		var value = controller.Step( true, mirrored, physical.LateralTilt,
			physical.SpinSpeed, physical.MovementCategory );
		_attributes.WriteScalar( outputName, value.Value, false, -1 );
	}

	private int Create()
	{
		var controller = _createController();
		ArgumentNullException.ThrowIfNull( controller );
		for ( var index = 0; index < _instances.Count; index++ )
		{
			if ( _instances[ index ] is not null )
				continue;
			_instances[ index ] = controller;
			_references[ index ] = 1;
			return index;
		}
		var instance = _instances.Count;
		_instances.Add( controller );
		_references.Add( 1 );
		return instance;
	}

	internal void Retain( int instance )
	{
		Retained( instance );
		_references[ instance ]++;
	}

	internal void Release( int instance )
	{
		Retained( instance );
		if ( --_references[ instance ] == 0 )
			_instances[ instance ] = null;
	}

	private TiltParameterController Retained( int instance )
	{
		if ( (uint)instance >= _instances.Count || _instances[ instance ] is null )
			throw new InvalidOperationException( "The tilt instance is not retained." );
		return _instances[ instance ];
	}
}
