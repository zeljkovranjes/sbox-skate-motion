namespace SkateMotion.Core.Collision;

internal interface ICollisionContactOutput
{
	/// <summary>Publishes complete contact records in their supplied order.</summary>
	void Write( ReadOnlySpan<uint> records );
}
