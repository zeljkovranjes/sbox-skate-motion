namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using static LimbSolverMath;

/// <summary>Retains authored board alignment and independently decays its vertical offset per update.</summary>
internal sealed class BoardAttachmentController
{
	internal LimbRigidTransform Frame { get; set; } = LimbRigidTransform.Identity;
	internal float FrameRemaining { get; set; }
	internal float HeightRemaining { get; set; }
	internal bool FrameRequested { get; set; }
	internal bool HeightRequested { get; set; }

	internal void RequestFromHand( LimbRigidTransform hand, LimbRigidTransform helper )
	{
		RequestFrame( hand.Compose( helper.RigidInverse() ) );
		RequestHeight( Frame.Translation.Y, 15f );
	}

	internal void RequestFrame( LimbRigidTransform frame )
	{
		Frame = frame;
		FrameRemaining = 15f;
		FrameRequested = true;
	}

	internal void RequestHeight( float height, float updates )
	{
		Frame = Frame with { Translation = new Vector3( Frame.Translation.X, height, Frame.Translation.Z ) };
		HeightRemaining = updates;
		HeightRequested = true;
	}

	/// <summary>Modifies the current animation's board and four authored targets in rig coordinates.</summary>
	internal void Update( ref LimbRigidTransform board, LimbRigidTransform[] authored )
	{
		ArgumentNullException.ThrowIfNull( authored );
		if ( authored.Length != 4 )
			throw new ArgumentException( "Board attachment requires four authored targets." );

		var active = !(FrameRemaining <= 0f);
		var decayedFrame = active && !FrameRequested;
		if ( decayedFrame )
		{
			DecayFrame();
			FrameRemaining -= 1f;
		}

		// Preserve the ordered comparison used after the frame-decay branch.
		var activeHeight = decayedFrame ? HeightRemaining > 0f : !(HeightRemaining <= 0f);
		if ( activeHeight )
		{
			active = true;
			if ( !HeightRequested )
			{
				var position = Frame.Translation;
				position.Y = MultiplyAdd( position.Y, DecayWeight( HeightRemaining ), 0f );
				Frame = Frame with { Translation = position };
				HeightRemaining -= 1f;
			}
		}

		if ( active )
		{
			board = Frame.Compose( board );
			for ( var i = 0; i < authored.Length; i++ )
				authored[ i ] = Frame.Compose( authored[ i ] );
		}
		FrameRequested = false;
		HeightRequested = false;
	}

	private void DecayFrame()
	{
		var weight = DecayWeight( FrameRemaining );
		var complement = 1f - weight;
		var rotation = new LimbFrame(
			Scale( Frame.Rotation.X, weight ) + Scale( Vector3.UnitX, complement ),
			Scale( Frame.Rotation.Y, weight ) + Scale( Vector3.UnitY, complement ),
			Scale( Frame.Rotation.Z, weight ) + Scale( Vector3.UnitZ, complement ) );
		var position = Scale( Frame.Translation, weight );
		position.Y = Frame.Translation.Y;
		Frame = new LimbRigidTransform( rotation.Orthonormalized(), position );
	}

	private static float DecayWeight( float remaining )
	{
		var fraction = (remaining - 1f) / remaining;
		return fraction * fraction;
	}
}
