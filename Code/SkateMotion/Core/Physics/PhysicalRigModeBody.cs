using SkateMotion.Core.Animation;

namespace SkateMotion.Core.Physics;

/// <summary>Retains a body's two mass variants and its aggregate shape properties.</summary>
internal sealed class PhysicalRigModeBody
{
	internal PhysicalBodyState Body { get; }
	internal PhysicalBodyMass Primary { get; }
	internal PhysicalBodyMass Alternate { get; }
	internal float MaximumInertia { get; set; }
	internal float LinearDamping { get; set; }
	internal float AngularDamping { get; set; }
	internal uint ShapeFlags { get; set; }
	internal uint ShapeMode { get; set; }
	internal uint CollisionCounter { get; set; }
	internal int ContactPropertySlot { get; set; }

	internal PhysicalRigModeBody( PhysicalBodyState body, PhysicalBodyMass primary, PhysicalBodyMass alternate )
	{
		ArgumentNullException.ThrowIfNull( body );
		if ( body.Mass is null )
			throw new ArgumentException( "Mode transitions require retained body mass properties.", nameof( body ) );
		Body = body;
		Primary = primary;
		Alternate = alternate;
	}

	internal void RefreshMass( PhysicalBodyMass mass, bool refreshMaximum )
	{
		Body.Mass = mass;
		if ( refreshMaximum )
		{
			var inertia = mass.InverseInertia;
			var minimum = inertia.Y > inertia.X ? inertia.X : inertia.Y;
			minimum = inertia.Z > minimum ? minimum : inertia.Z;
			MaximumInertia = 1f / minimum;
		}
		// A mode switch retains the current basis and orientation without normalizing again.
		PhysicalBodyBinding.UpdateInertia( Body, Body.Pose.Rotation, mass );
	}
}
