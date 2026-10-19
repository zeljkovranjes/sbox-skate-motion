namespace SkateMotion;

/// <summary>On-demand diagnostic snapshot: independent body states, largest joint-anchor error in metres and kinetic energy in joules.</summary>
public sealed record SkateTelemetry( SkateBodyState[] Bodies, SkateContactState[] Contacts,
	float MaximumJointError, float KineticEnergy );
