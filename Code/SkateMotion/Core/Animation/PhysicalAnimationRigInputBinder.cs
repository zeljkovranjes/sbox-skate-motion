namespace SkateMotion.Core.Animation;

/// <summary>Binds retained animation poses and contact flags to the physical rig's input stage.</summary>
internal sealed class PhysicalAnimationRigInputBinder
{
	private readonly EffectorPoseInput[] _poses = new EffectorPoseInput[ 4 ];

	internal PhysicalRigInput Bind( PhysicalAnimationInputState state, PhysicalRigInput supplied )
	{
		ArgumentNullException.ThrowIfNull( state );
		if ( supplied.RigInput.Poses is null || supplied.RigInput.Poses.Length != 4 )
			throw new ArgumentException( "Physical rig binding requires four pose inputs." );
		var pose = state.Pose;
		var attributes = pose.Attributes;
		for ( var i = 0; i < _poses.Length; i++ )
			_poses[ i ] = supplied.RigInput.Poses[ i ] with { Authored = pose.Authored[ i ] };
		return supplied with
		{
			Frames = supplied.Frames with { Solver = pose.CachedRoot, AnimatedRoot = pose.CachedBoard },
			RigInput = supplied.RigInput with
			{
				Poses = _poses,
				FeetEnabled = state.FootActivation != 0,
				LeftHandEnabled = ( attributes.ActionFlags & (1UL << 39) ) != 0,
				RightHandEnabled = ( attributes.ActionFlags & (1UL << 40) ) != 0,
				DisableFeet = ( attributes.RigFlags & (1UL << 59) ) != 0,
				SelectedContactId = attributes.ContactBone,
				ReactionEnabled = ( attributes.MotionFlags & ((1UL << 25) | (1UL << 30)) ) != 0,
				RequestSuppressed = ( attributes.MotionFlags & ((1UL << 24) | (1UL << 23)) ) != 0
			}
		};
	}
}
