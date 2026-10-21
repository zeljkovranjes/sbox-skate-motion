namespace SkateMotion.Core.Animation;

/// <summary>
/// Graph data giving the size of the board the clips were made on (metres), so their feet and hands are fitted onto
/// the board ridden (see <see cref="BoardFit"/>). A value left at zero keeps the default board's.
/// </summary>
internal sealed class ClipBoardDefinition
{
	/// <summary>Distance between the clips' board's axles.</summary>
	public float Wheelbase { get; set; }

	/// <summary>The clips' board's length, nose to tail.</summary>
	public float Length { get; set; }

	/// <summary>The clips' board's width.</summary>
	public float Width { get; set; }

	/// <summary>The clips' board's deck thickness.</summary>
	public float Thickness { get; set; }
}
