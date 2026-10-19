using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Input;

/// <summary>Produces one ordered request set from retained samples and caller-owned movement context.</summary>
internal sealed partial class ControllerActionRouter
{
	private readonly LimbArcTangent _arcTangent;
	private readonly ObjectMovementInputProcessor _objectMovement;
	private bool _holdPatternPending;
	internal ControllerInputState State { get; } = new();
	internal InputRequestBuffer Requests { get; }
	internal ControllerActionTuning Tuning { get; set; }

	internal bool HoldPatternPending
	{
		get => _holdPatternPending;
		set => _holdPatternPending = value;
	}

	internal ControllerActionRouter( LimbArcTangent arcTangent, ObjectMovementInputProcessor objectMovement,
		ControllerActionTuning tuning, int requestCapacity )
	{
		ArgumentNullException.ThrowIfNull( arcTangent );
		ArgumentNullException.ThrowIfNull( objectMovement );
		_arcTangent = arcTangent;
		_objectMovement = objectMovement;
		Tuning = tuning;
		Requests = new( requestCapacity );
	}

	internal void Update( ControllerReading reading, float delta, ControllerActionContext context,
		QueuedInputCommand[] commands, int commandCount )
	{
		ArgumentNullException.ThrowIfNull( commands );
		if ( commandCount < 0 || commandCount > commands.Length )
			throw new ArgumentOutOfRangeException( nameof( commandCount ) );
		Requests.Clear();
		ControllerInputProcessor.Update( State, reading, delta, Tuning.ActivityThreshold, context.RetainPreviousBumper );
		var current = State.Current;
		var sticks = ControllerStickValues.Evaluate( current, _arcTangent );
		var buttons = RidingButtonActions.Evaluate( State, context.Buttons, Tuning.Buttons, sticks.LeftAngle, sticks.LeftMagnitude );
		var analog = RidingAnalogProcessor.Evaluate( current, sticks, context.Analog, Tuning.Analog );
		EmitAuxiliary( context );
		EmitOffBoard( current );
		var movement = _objectMovement.Evaluate( current, sticks.HasLeft, sticks.LeftMagnitude );
		Emit( _objectForward, true, movement.Forward );
		Emit( _objectSideways, true, movement.Sideways );
		Emit( _objectRotation, true, movement.Rotation );
		EmitRiding( buttons, analog );
		var walking = WalkingInputActions.Evaluate( current, context.Walking );
		Emit( _walkingMagnitude, true, walking.Magnitude );
		Emit( _walkingZ, true, walking.WorldZ );
		Emit( _walkingX, true, walking.WorldX );
		Emit( _walkingStickMagnitude, true, walking.StickMagnitude );
		Emit( _holdPattern, QueuedInputProcessor.ConsumeHoldPattern( ref _holdPatternPending ) );
		var queueContext = context.Queue with
		{
			HasAirGrab = buttons.LeftAirGrab || buttons.RightAirGrab,
			Pushing = buttons.Pushing
		};
		for ( var i = 0; i < commandCount; i++ )
		{
			var result = QueuedInputProcessor.Evaluate( commands[ i ], queueContext );
			if ( !result.HasCommand )
				continue;
			Emit( _trick, result.IsTrick );
			Requests.Add( result.Key, 1f );
			Emit( _gestureSpeed, result.IsTrick, result.Speed );
		}
	}

	private void Emit( InputCommandKey key, bool present, float value = 1f )
	{
		if ( present )
			Requests.Add( key, value );
	}

	private void EmitAxis( InputCommandKey key, float value ) => Emit( key, value != 0f, value );
}
