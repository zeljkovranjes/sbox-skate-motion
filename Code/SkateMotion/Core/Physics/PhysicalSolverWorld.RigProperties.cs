namespace SkateMotion.Core.Physics;

internal sealed partial class PhysicalSolverWorld
{
	internal void RefreshRigBodyProperties( PhysicalRigBodyModes modes )
	{
		ValidateRigBodyProperties( modes );
		foreach ( var body in modes.Bodies )
		{
			var index = IndexOfBody( body.Body );
			_settings[ index ] = _settings[ index ] with
			{
				LocalInverseInertia = body.Body.Mass.Value.InverseInertia,
				LinearDamping = body.LinearDamping,
				AngularDamping = body.AngularDamping,
				AngularMotionWeight = body.MaximumInertia
			};
		}
	}

	internal void ValidateRigBodyProperties( PhysicalRigBodyModes modes )
	{
		ArgumentNullException.ThrowIfNull( modes );
		if ( _prepared )
			throw new InvalidOperationException( "Apply rig properties between solver steps, before preparing constraints." );
		foreach ( var body in modes.Bodies )
		{
			if ( IndexOfBody( body.Body ) < 0 || body.Body.Mass is null )
				throw new ArgumentException( "Every rig body must belong to this world and retain its mass properties." );
		}
	}
}
