namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;

/// <summary>
/// A wheel found in a model (board frame, before the deck's middle is made the origin): its centre and radius, the
/// stretch across the board it spans, and whether it is a piece of mesh of its own.
/// </summary>
internal readonly record struct WheelFit( Vector3 Centre, float Radius, float MinY, float MaxY, bool Piece )
{
	// Share of the radius around the axle that holds the axle and hub rather than the wheel's tyre.
	private const float CoreShare = 0.35f;

	// Slack (share of the radius) past the wheel's outline still counted as the wheel.
	private const float OutlineSlack = 1.08f;

	internal float Width => MaxY - MinY;

	/// <summary>How far a point lies from the axle, seen along it.</summary>
	internal float AxleDistance( Vector3 point )
	{
		var dx = point.X - Centre.X;
		var dz = point.Z - Centre.Z;
		return MathF.Sqrt( dx * dx + dz * dz );
	}

	/// <summary>True for a point inside the wheel's outline (axle and hub included).</summary>
	internal bool Holds( Vector3 point ) => point.Y >= MinY && point.Y <= MaxY && AxleDistance( point ) <= Radius * OutlineSlack;

	/// <summary>True for a point of the wheel's tyre: inside its outline, away from the axle and hub.</summary>
	internal bool Tyre( Vector3 point ) => Holds( point ) && AxleDistance( point ) >= Radius * CoreShare;

	/// <summary>True for a point within the wheel's outline seen along its axle, wherever it lies across the board.</summary>
	internal bool Within( Vector3 point ) => AxleDistance( point ) <= Radius * OutlineSlack;
}
