namespace SkateMotion.Core.BoardModel;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>
/// Which way a skateboard model lies: along its longest side, up its shortest, with the deck on top. Its nose and tail
/// look alike, so the positive end of the long side is taken as the nose.
/// </summary>
internal static class BoardFrame
{
	// Share of the length at each end that holds only deck (the nose and tail), whose height says which way is up.
	private const float EndShare = 0.08f;

	/// <summary>The rotation from the model's frame into the board's (+X along the deck, +Y left, +Z up).</summary>
	internal static Quaternion Find( Vector3[] points )
	{
		var min = new Vector3( float.MaxValue );
		var max = new Vector3( float.MinValue );
		foreach ( var point in points )
		{
			min = Vector3.Min( min, point );
			max = Vector3.Max( max, point );
		}
		var size = max - min;
		var along = Longest( size );
		var up = Shortest( size, along );
		var forward = Axis( along );
		var upward = Axis( up );
		if ( !DeckOnTop( points, min, max, along, up ) )
			upward = -upward;
		var left = Vector3.Cross( upward, forward );
		// Rows of the turn are the board's axes as seen in the model: forward, left and up land on X, Y and Z.
		var turn = new Matrix4x4(
			forward.X, left.X, upward.X, 0f,
			forward.Y, left.Y, upward.Y, 0f,
			forward.Z, left.Z, upward.Z, 0f,
			0f, 0f, 0f, 1f );
		return Quaternion.Normalize( Quaternion.CreateFromRotationMatrix( turn ) );
	}

	private static int Longest( Vector3 size ) => size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;

	private static int Shortest( Vector3 size, int along )
	{
		var shortest = -1;
		for ( var axis = 0; axis < 3; axis++ )
		{
			if ( axis != along && (shortest < 0 || Component( size, axis ) < Component( size, shortest )) )
				shortest = axis;
		}
		return shortest;
	}

	// The deck at the nose and tail sits above the middle of the board's height: wheels and trucks hang below it.
	private static bool DeckOnTop( Vector3[] points, Vector3 min, Vector3 max, int along, int up )
	{
		var length = Component( max - min, along );
		var middle = (Component( min, up ) + Component( max, up )) * 0.5f;
		var sum = 0f;
		var count = 0;
		foreach ( var point in points )
		{
			var position = Component( point, along );
			if ( position > Component( min, along ) + length * EndShare && position < Component( max, along ) - length * EndShare )
				continue;
			sum += Component( point, up );
			count++;
		}
		return count == 0 || sum / count >= middle;
	}

	private static Vector3 Axis( int axis ) => axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ;

	private static float Component( Vector3 value, int axis ) => axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
}
