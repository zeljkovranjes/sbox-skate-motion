namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;

/// <summary>Contact selection and pin history accompanying the shared rig feedback.</summary>
internal sealed class PhysicalContactFeedbackState
{
	internal PhysicalRigFeedbackState Feedback { get; }
	internal Vector3[] Normals { get; } = new Vector3[ 20 ];
	internal int[] Bones { get; } = new int[ 20 ];
	internal PhysicalContactPin[] Pins { get; } = [new(), new()];
	internal bool AnyContact { get; set; }

	internal PhysicalContactFeedbackState( PhysicalRigFeedbackState feedback )
	{
		ArgumentNullException.ThrowIfNull( feedback );
		Feedback = feedback;
	}
}
