namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Produces retained contact descriptors and metadata before movement classification.</summary>
internal sealed class AnimationContactHistory
{
	private readonly AnimationContactFrame[] _frames = new AnimationContactFrame[ 30 ];
	private readonly int[] _frameTicks = new int[ 30 ];
	internal AnimationContactFrame EntryFrame { get; private set; } = AnimationContactFrame.Initial;
	internal AnimationContactDescriptor Candidate { get; private set; } = AnimationContactDescriptor.Initial;
	internal AnimationContactDescriptor Current { get; private set; } = AnimationContactDescriptor.Initial;
	internal AnimationContactDescriptor Confirmed { get; private set; } = AnimationContactDescriptor.Initial;
	internal int PreviousParent { get; private set; }
	internal int PreviousSubtype { get; private set; } = -1;
	internal int EntrySide { get; private set; }
	internal int PositionSide { get; private set; }
	internal int FacingSide { get; private set; } = 4;
	internal bool ReversedEntry { get; private set; }
	internal int AwayFrames { get; private set; } = 31;
	internal int StableFrames { get; private set; }
	internal int Tick { get; private set; }
	internal int FrameCursor { get; private set; }
	internal AnimationContactHistoryOutput Output { get; private set; }

	internal AnimationContactHistory() => Array.Fill( _frames, AnimationContactFrame.Initial );

	internal AnimationContactFrame FrameAt( int index ) => _frames[ index ];
	internal int FrameTickAt( int index ) => _frameTicks[ index ];

	internal void Reset()
	{
		PreviousParent = 0;
		PreviousSubtype = -1;
		EntrySide = PositionSide = StableFrames = Tick = FrameCursor = 0;
		FacingSide = 4;
		AwayFrames = 31;
		ReversedEntry = false;
	}

	internal AnimationContactHistoryOutput Update( AnimationContactHistoryInput input,
		AnimationContactMetadataTable table )
	{
		ArgumentNullException.ThrowIfNull( table );
		Tick = unchecked( Tick + 1 );
		PrepareEntry( input );
		if ( !input.Active )
		{
			PreviousParent = input.ParentController;
			ReversedEntry = false;
			FacingSide = 4;
			return Output;
		}
		var descriptor = Describe( input );
		if ( PreviousParent != 400 )
		{
			Candidate = descriptor;
			StableFrames = 13;
			Current = Candidate;
			Confirmed = Candidate;
		}
		else if ( descriptor != Candidate )
		{
			Candidate = descriptor;
			StableFrames = 0;
		}
		else
		{
			StableFrames = unchecked( StableFrames + 1 );
			if ( StableFrames > 0 )
				Current = Candidate;
			if ( StableFrames > 12 )
				Confirmed = Candidate;
		}
		PreviousParent = input.ParentController;
		var contact = input.Contact with
		{
			Kind = table.Kind( Confirmed ), FirstKey = table.Key( Current ), SecondKey = table.Key( Confirmed )
		};
		Output = new( Candidate, Current, Confirmed, table.Index( Current ), table.Index( Confirmed ), contact );
		return Output;
	}

	internal AnimationPhysicalMovementOutput UpdateMovement( AnimationContactHistoryInput input,
		AnimationContactMetadataTable table, AnimationPhysicalMovementState movement,
		AnimationPhysicalMovementInput movementInput )
	{
		ArgumentNullException.ThrowIfNull( movement );
		var output = Update( input, table );
		return movement.Update( movementInput with { Contact = input.Active ? output.Contact : movementInput.Contact } );
	}

	private void PrepareEntry( AnimationContactHistoryInput input )
	{
		if ( input.ResetFrames )
		{
			EntryFrame = input.Frame;
			FrameCursor = 0;
		}
		if ( input.ParentController == 100 )
		{
			var index = FrameCursor % 30;
			_frames[ index ] = input.Frame;
			_frameTicks[ index ] = Tick;
			FrameCursor = FrameCursor == 59 ? 30 : FrameCursor + 1;
		}
		var retained = FrameCursor > 29 ? _frames[ (FrameCursor - 30) % 30 ] : EntryFrame;
		if ( input.ParentController == 400 )
		{
			if ( PreviousParent != 400 && AwayFrames > 30 )
			{
				EntryFrame = retained;
				var side = Dot( input.ContactSide, EntryFrame.Position - input.ContactPosition ) > 0f
					? input.ContactSide : -input.ContactSide;
				var forward = Dot( EntryFrame.MotionFirst + EntryFrame.MotionSecond, EntryFrame.Forward ) > 0f
					? EntryFrame.Forward : -EntryFrame.Forward;
				EntrySide = Dot( forward, side ) > 0f ? 1 : 0;
			}
			else if ( PreviousSubtype != input.Contact.Index )
				EntryFrame = retained;
		}
		PositionSide = (uint)(input.Contact.Index - 2) <= 2
			&& !(Dot( input.ContactPosition - input.ReferencePosition, input.ReferenceAxis ) > 0f) ? 1 : 0;
		AwayFrames = input.ParentController == 400 ? 0 : unchecked( AwayFrames + 1 );
		PreviousSubtype = input.Contact.Index;
	}

	private AnimationContactDescriptor Describe( AnimationContactHistoryInput input )
	{
		var motion = input.Frame.MotionFirst + input.Frame.MotionSecond;
		var forward = Dot( motion, input.Frame.Forward ) > 0f ? input.Frame.Forward : -input.Frame.Forward;
		var facing = Dot( forward, input.ContactDirection ) > 0f;
		if ( input.Contact.Index == 0 || input.Contact.Index == 3 )
		{
			if ( FacingSide == 4 )
			{
				var retained = FrameCursor > 29 ? _frames[ (FrameCursor - 30) % 30 ] : EntryFrame;
				ReversedEntry = Dot( motion, retained.MotionFirst + retained.MotionSecond ) < 0f;
			}
			FacingSide = facing ? (ReversedEntry ? 2 : 0) : (ReversedEntry ? 3 : 1);
		}
		else
			FacingSide = facing ? 0 : 1;
		var side = Dot( input.Frame.Position - input.ContactPosition, input.BodySide ) > 0f
			? input.BodySide : -input.BodySide;
		var normalSide = Dot( input.ContactNormal, side ) <= -.1f ? 1 : 0;
		var projected = input.Motion - Scale( input.ContactNormal, Dot( input.Motion, input.ContactNormal ) );
		var length = SquareRoot( Dot( projected, projected ) );
		var along = 1;
		if ( length > .01f )
		{
			var direction = Scale( projected, Reciprocal( length ) );
			var dot = Dot( direction, input.ContactDirection );
			if ( dot <= 0f )
				dot = Dot( input.ContactDirection, -direction );
			along = dot <= .9397f ? 0 : 1;
		}
		return new( EntrySide, PositionSide, along, normalSide, FacingSide, input.Contact.Index );
	}
}
