using Sandbox;

namespace SkateMotion.Engine;

internal static class BoxRails
{
	/// <summary>Local grind edges along the long top axis of a box: its center line when narrow, both long edges when wide.</summary>
	internal static int TopEdges( Vector3 center, Vector3 size, float railWidth, Span<(Vector3 Start, Vector3 End)> output )
	{
		var alongX = size.x >= size.y;
		var length = alongX ? size.x : size.y;
		var width = alongX ? size.y : size.x;
		var axis = alongX ? Vector3.Forward : Vector3.Left;
		var across = alongX ? Vector3.Left : Vector3.Forward;
		var top = center + Vector3.Up * (size.z * 0.5f);
		var half = axis * (length * 0.5f);
		if ( width <= railWidth || output.Length < 2 )
		{
			output[ 0 ] = (top - half, top + half);
			return 1;
		}
		var edge = across * (width * 0.5f);
		output[ 0 ] = (top + edge - half, top + edge + half);
		output[ 1 ] = (top - edge - half, top - edge + half);
		return 2;
	}
}
