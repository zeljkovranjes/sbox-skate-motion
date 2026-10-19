namespace SkateMotion.Core.Animation;

/// <summary>Orders hand, motion and vertical requests before applying retained board alignment.</summary>
internal sealed class BoardAttachmentSequence
{
	private readonly GrindAttachmentController _grind;
	private readonly DescendingAttachmentController _descending;
	private readonly VerticalAttachmentController _vertical;
	private readonly BoardAttachmentController _attachment;

	internal BoardAttachmentSequence( GrindAttachmentController grind, DescendingAttachmentController descending,
		VerticalAttachmentController vertical, BoardAttachmentController attachment )
	{
		ArgumentNullException.ThrowIfNull( grind );
		ArgumentNullException.ThrowIfNull( descending );
		ArgumentNullException.ThrowIfNull( vertical );
		ArgumentNullException.ThrowIfNull( attachment );
		_grind = grind;
		_descending = descending;
		_vertical = vertical;
		_attachment = attachment;
	}

	/// <summary>Accepts freshly gathered board/targets; the caller updates its pose cache after this returns.</summary>
	internal void Update( BoardAttachmentInput input, GrindApproachState state,
		ref LimbRigidTransform board, LimbRigidTransform[] authored )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( authored );
		if ( authored.Length != 4 )
			throw new ArgumentException( "Board attachment requires four authored targets." );

		if ( input.AttachFromHand )
			_attachment.RequestFromHand( input.Hand, input.Helper );

		if ( !input.DescendingMode )
			_grind.Update( input.Grind with { MutableBoard = board }, state, _attachment );
		else if ( !input.SuppressDescending )
			_descending.Update( input.Descending with { Board = board }, _attachment );

		// A bypass leaves grind history intact. Vertical requests can replace height
		// without replacing the frame requested by either preceding stage.
		_vertical.Update( input.Vertical, _attachment );
		_attachment.Update( ref board, authored );
	}
}
