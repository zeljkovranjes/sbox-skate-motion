namespace SkateMotion;

/// <summary>Recognized gestures; recognition does not imply successful landing.</summary>
public enum SkateTrick
{
	/// <summary>No new gesture.</summary>
	None,
	/// <summary>Tail pop.</summary>
	Ollie,
	/// <summary>Nose pop.</summary>
	Nollie,
	/// <summary>Outward board flip.</summary>
	Kickflip,
	/// <summary>Inward board flip.</summary>
	Heelflip,
	/// <summary>Horizontal board spin.</summary>
	Shuvit,
	/// <summary>Kickflip with a half spin.</summary>
	VarialKickflip,
	/// <summary>Heelflip with a half spin.</summary>
	VarialHeelflip,
	/// <summary>Kickflip with a frontside half spin.</summary>
	Hardflip,
	/// <summary>Heelflip with a backside half spin.</summary>
	InwardHeelflip,
	/// <summary>Kickflip with a full spin.</summary>
	Treflip,
	/// <summary>Heelflip with a full frontside spin.</summary>
	Laserflip,
	/// <summary>Frontside horizontal half spin.</summary>
	FrontsideShuvit,
	/// <summary>Full horizontal spin.</summary>
	Shuvit360,
	/// <summary>Full frontside horizontal spin.</summary>
	FrontsideShuvit360
}
