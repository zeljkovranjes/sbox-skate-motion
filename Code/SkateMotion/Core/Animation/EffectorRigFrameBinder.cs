namespace SkateMotion.Core.Animation;

/// <summary>Selects supplied frames or derives targeting frames from the animated root.</summary>
internal static class EffectorRigFrameBinder
{
	internal static EffectorRigFrameSet Bind( EffectorRigFrameSources source, bool useAnimatedRootFrames )
	{
		var authored = source.Authored;
		var contact = source.Contact;
		var offset = source.Offset;
		if ( useAnimatedRootFrames )
		{
			authored = source.ModelToWorld.Compose( source.AnimatedRoot );
			contact = authored.RigidInverse();
			offset = authored;
		}
		return new EffectorRigFrameSet( source.ModelToWorld, source.WorldToModel, authored, contact,
			source.Solver, offset, source.ReferencePosition, source.MotionDelta );
	}
}
