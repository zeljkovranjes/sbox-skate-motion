namespace SkateMotion.Core.Animation;

/// <summary>Classifies physical movement while preserving transition counters and contact history.</summary>
internal sealed class AnimationPhysicalMovementState
{
	private readonly AnimationMovementContactKey _emptyKey;
	internal int Current { get; private set; }
	internal int Previous { get; private set; }
	internal int PreviousController { get; private set; }
	internal int AirFrames { get; private set; }
	internal int SpecialFrames { get; private set; }
	internal int WithoutContactFrames { get; private set; }
	internal int WithContactFrames { get; private set; }
	internal int SinceBoardEventFrames { get; private set; }
	internal bool TransitionRequested { get; private set; }
	internal float RetainedValue { get; set; }
	internal AnimationMovementContactData RetainedContact { get; private set; }

	internal AnimationPhysicalMovementState( AnimationMovementContactKey emptyKey )
	{
		_emptyKey = emptyKey;
		Reset();
	}

	internal void Reset()
	{
		Current = Previous = PreviousController = 0;
		AirFrames = SpecialFrames = WithoutContactFrames = WithContactFrames = SinceBoardEventFrames = 0;
		TransitionRequested = true;
		RetainedValue = 0f;
		RetainedContact = EmptyContact();
	}

	internal AnimationPhysicalMovementOutput Update( AnimationPhysicalMovementInput input )
	{
		Previous = Current;
		UpdateHistory( input );
		SelectState( input );
		PreviousController = input.Controllers.Current;
		var active = Current == 3;
		return new( Current, Previous, RetainedValue, active, active ? RetainedContact : EmptyContact() );
	}

	private void UpdateHistory( AnimationPhysicalMovementInput input )
	{
		TransitionRequested = Current is 0 or 4
			|| (PreviousController == 702 && input.Controllers.Current != 702);
		AirFrames = input.Controllers.Parent == 200 ? unchecked( AirFrames + 1 ) : 0;
		if ( input.Controllers.Current == 701 )
		{
			SpecialFrames = unchecked( SpecialFrames + 1 );
			WithoutContactFrames = input.BoardHasContacts ? 0 : unchecked( WithoutContactFrames + 1 );
			WithContactFrames = input.BoardHasContacts ? unchecked( WithContactFrames + 1 ) : 0;
		}
		else
		{
			SpecialFrames = WithoutContactFrames = WithContactFrames = 0;
		}
		SinceBoardEventFrames = input.Controllers.Parent == 100 && input.BoardContactType == 8
			? 0 : unchecked( SinceBoardEventFrames + 1 );
	}

	private void SelectState( AnimationPhysicalMovementInput input )
	{
		var code = input.Controllers.Current;
		if ( code >= 400 && code <= 405 )
		{
			RetainedContact = input.Contact;
			Current = 3;
			return;
		}
		switch ( code )
		{
			case 100:
				Current = input.PrimarySecondaryMode ? 2 : 1;
				break;
			case >= 101 and <= 105:
			case 602:
				Current = 1;
				break;
			case 200:
				SelectAirState( input.BoardHasContacts );
				break;
			case 201:
				Current = input.BoardHasContacts && !input.ContactTransition && AirFrames > 5 ? 1 : 2;
				break;
			case 202:
			case 600:
			case 601:
				Current = 2;
				break;
			case 300:
				Current = 4;
				break;
			case 500:
			case 502:
				Current = 6;
				break;
			case 501:
				Current = input.OffboardSecondaryMode ? 6 : 7;
				break;
			case 503:
				Current = input.OffboardRideMode ? 1 : 6;
				break;
			case 702:
				Current = 5;
				break;
		}
	}

	private void SelectAirState( bool hasContacts )
	{
		var persistent = AirFrames > 3;
		if ( Current == 2 )
			Current = (persistent && hasContacts) || TransitionRequested ? 1 : 2;
		else if ( Current == 1 )
		{
			if ( (persistent && SinceBoardEventFrames > 9) || TransitionRequested )
				Current = 2;
		}
		else if ( persistent || TransitionRequested )
			Current = 2;
	}

	private AnimationMovementContactData EmptyContact() => new(
		-1, -1, 0, 0f, ulong.MaxValue, ulong.MaxValue, _emptyKey, _emptyKey );
}
