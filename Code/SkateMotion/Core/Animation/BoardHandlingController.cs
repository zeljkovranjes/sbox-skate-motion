namespace SkateMotion.Core.Animation;

/// <summary>Retained board interaction phases driven by physical ownership and actual channel playback.</summary>
internal sealed class BoardHandlingController
{
	private BoardHandlingPhase _phase;
	private float _previousYaw;
	private float _yaw;
	private float _pitch;
	private bool _initializedYaw;
	private bool _back;
	private bool _throwing;

	internal BoardHandlingPhase Phase => _phase;

	internal void Reset()
	{
		_phase = BoardHandlingPhase.Idle;
		ResetOrientation();
	}

	internal BoardHandlingResult Update( in BoardHandlingInput input, in BoardHandlingPlayback playback )
	{
		if ( !input.GrabbingObject && _phase == BoardHandlingPhase.Idle )
		{
			if ( input.DropRequested && input.HoldingBoard )
			{
				_phase = BoardHandlingPhase.DropStart;
				_throwing = false;
			}
			else if ( input.ThrowRequested && input.HoldingBoard )
			{
				_phase = BoardHandlingPhase.DropStart;
				_throwing = true;
			}
			else if ( input.RetrieveRequested && !input.HoldingBoard && !input.RetrievalBlocked )
			{
				_phase = BoardHandlingPhase.RetrieveStart;
			}
		}
		if ( _phase == BoardHandlingPhase.Idle )
		{
			ResetOrientation();
			return default;
		}
		return _phase is BoardHandlingPhase.DropStart or BoardHandlingPhase.DropPlaying
			? Drop( playback ) : Retrieve( input, playback );
	}

	private BoardHandlingResult Drop( in BoardHandlingPlayback playback )
	{
		var command = BoardHandlingCommand.None;
		var motion = BoardHandlingMotion.None;
		var blend = 0f;
		if ( _phase == BoardHandlingPhase.DropStart )
		{
			_phase = BoardHandlingPhase.DropPlaying;
			command = BoardHandlingCommand.Transition;
			motion = _throwing ? BoardHandlingMotion.Throw : BoardHandlingMotion.Drop;
			blend = 0.25f;
		}
		else if ( !playback.Exists || playback.Remaining <= 0f || !(playback.Elapsed <= 0.5f) )
		{
			_phase = BoardHandlingPhase.Idle;
			command = BoardHandlingCommand.Stop;
			blend = 1f / 6f;
		}
		return new( false, true, false, false, 0f, 0f, command, motion, blend );
	}

	private BoardHandlingResult Retrieve( in BoardHandlingInput input, in BoardHandlingPlayback playback )
	{
		var command = BoardHandlingCommand.None;
		var motion = BoardHandlingMotion.None;
		var retrieve = false;
		var blend = 0f;
		if ( _phase != BoardHandlingPhase.RetrieveOut )
			UpdateOrientation( input );
		switch ( _phase )
		{
			case BoardHandlingPhase.RetrieveStart:
				_phase = BoardHandlingPhase.RetrieveInto;
				command = BoardHandlingCommand.Transition;
				motion = _back ? BoardHandlingMotion.BackInto : BoardHandlingMotion.FrontInto;
				break;
			case BoardHandlingPhase.RetrieveInto:
				if ( !playback.Exists || playback.Remaining <= 0f )
					_phase = BoardHandlingPhase.Idle;
				else if ( !(playback.Remaining > 0.25f) )
					retrieve = true;
				break;
			case BoardHandlingPhase.RetrieveCycle:
				if ( !input.RetrievalActive )
					command = BoardHandlingCommand.Stop;
				else if ( !playback.Exists || playback.Remaining <= 0f )
					_phase = BoardHandlingPhase.Idle;
				else if ( input.HoldingBoard )
				{
					_phase = BoardHandlingPhase.RetrieveOut;
					command = BoardHandlingCommand.Transition;
					motion = _back ? BoardHandlingMotion.BackOut : BoardHandlingMotion.FrontOut;
				}
				else if ( !(playback.Remaining > 0.25f) )
					retrieve = true;
				break;
			case BoardHandlingPhase.RetrieveOut:
				if ( !playback.Exists || playback.Remaining <= 0f || playback.Elapsed > 1f / 6f )
					command = BoardHandlingCommand.Stop;
				break;
		}
		if ( retrieve )
		{
			_phase = BoardHandlingPhase.RetrieveCycle;
			command = BoardHandlingCommand.Sequence;
			motion = _back ? BoardHandlingMotion.BackCycle : BoardHandlingMotion.FrontCycle;
		}
		if ( command == BoardHandlingCommand.Stop )
		{
			_phase = BoardHandlingPhase.Idle;
			blend = 1f / 6f;
		}
		else if ( command != BoardHandlingCommand.None )
		{
			blend = 0.25f;
		}
		return new( true, false, retrieve, true, _yaw, _pitch, command, motion, blend );
	}

	private void ResetOrientation()
	{
		_previousYaw = 0f;
		_yaw = 0f;
		_pitch = 0f;
		_initializedYaw = false;
		_back = false;
	}

	private void UpdateOrientation( in BoardHandlingInput input )
	{
		var yaw = input.YawRadians * -57.295776f;
		if ( input.Mirrored ) yaw = -yaw;
		if ( yaw < -90f )
		{
			yaw = _initializedYaw
				? (_yaw >= 0f ? 180f : -90f)
				: (yaw >= -135f ? -90f : 180f);
		}
		if ( input.Mirrored ) yaw = -yaw;
		if ( _initializedYaw )
		{
			// Five degrees per invocation, independent of elapsed time. Preserve the ordered selects.
			var difference = yaw - _previousYaw;
			var lower = -5f - difference >= 0f ? -5f : difference;
			var step = 5f - lower >= 0f ? lower : 5f;
			yaw = _previousYaw + step;
		}
		_previousYaw = yaw;
		_yaw = input.Mirrored ? -yaw : yaw;
		_pitch = input.PitchRadians * 57.295776f;
		_initializedYaw = true;
		_back = MathF.Abs( _yaw ) > 90f;
	}
}
