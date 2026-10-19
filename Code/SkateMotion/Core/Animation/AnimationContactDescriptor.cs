namespace SkateMotion.Core.Animation;

/// <summary>Retains the six discrete fields used to resolve contact metadata.</summary>
internal readonly record struct AnimationContactDescriptor(
	int EntrySide, int PositionSide, int AlongContact, int NormalSide, int FacingSide, int Subtype )
{
	internal static AnimationContactDescriptor Initial => new( 0, 0, 0, 0, 0, -1 );
	internal int TableIndex => unchecked( EntrySide * 192 + PositionSide * 96
		+ AlongContact * 48 + NormalSide * 24 + FacingSide * 6 + Subtype );
}
