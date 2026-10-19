namespace SkateMotion.Core.Animation;

/// <summary>Refreshes angular ranges while retaining structural frames, speeds and modes.</summary>
internal static class PhysicalStructuralJointRanges
{
	internal static PhysicalStructuralJoint Refresh( PhysicalStructuralJoint joint, PhysicalStructuralBone bone,
		PhysicalJointRangeScales scales, bool alternate, float swingMultiplier, float twistMultiplier,
		LimbSolverMath math )
	{
		ArgumentNullException.ThrowIfNull( math );
		var swing = bone.SwingAngle * (alternate ? scales.AlternateSwing : scales.PrimarySwing);
		var twist = bone.TwistAngle * (alternate ? scales.AlternateTwist : scales.PrimaryTwist);
		if ( alternate )
		{
			swing *= swingMultiplier;
			twist *= twistMultiplier;
		}
		// Refresh does not repeat construction's per-joint lock shortcut.
		swing = swing - .01f >= 0f ? swing : .01f;
		twist = twist - .01f >= 0f ? twist : .01f;
		return joint with
		{
			Limits = joint.Limits with
			{
				SwingAngle = swing,
				TwistAngle = twist,
				SwingCosine = math.SinCos( swing ).Cosine,
				TwistCosine = math.SinCos( twist ).Cosine
			}
		};
	}
}
