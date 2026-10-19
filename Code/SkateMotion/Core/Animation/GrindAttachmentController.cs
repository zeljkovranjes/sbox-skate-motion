namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Runs the complete supplied-input grind approach branch and requests retained board attachment.</summary>
internal sealed class GrindAttachmentController
{
	private readonly GrindApproachSolver _approach;
	private readonly GrindAngularCorrector _angular;
	private readonly GrindAttachmentFrameBuilder _frames;

	internal GrindAttachmentController( GrindApproachSolver approach, GrindAngularCorrector angular,
		GrindAttachmentFrameBuilder frames )
	{
		ArgumentNullException.ThrowIfNull( approach );
		ArgumentNullException.ThrowIfNull( angular );
		ArgumentNullException.ThrowIfNull( frames );
		_approach = approach;
		_angular = angular;
		_frames = frames;
	}

	internal void Update( GrindAttachmentInput input, GrindApproachState state, BoardAttachmentController attachment )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( attachment );
		state.RequestValid = false;
		if ( !input.Enabled )
			return;
		if ( input.Suppressed )
		{
			state.CorrectionPosition = Vector3.Zero;
			state.AngularPosition = Vector3.Zero;
			return;
		}
		_approach.Update( input.Approach, state );
		if ( !state.HasCandidate )
			return;
		_angular.Update( input.Approach.Board.Rotation, input.EdgeMetadata, state );
		var frame = _frames.Build( input, state );
		attachment.RequestFrame( frame );
		attachment.RequestHeight( frame.Translation.Y, 15f );
		state.RequestValid = true;
	}
}
