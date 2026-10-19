namespace SkateMotion.Core.Animation;

/// <summary>Math coefficients and explicit world policy supplied with a physical animation graph.</summary>
internal sealed class PhysicalAnimationRuntimeSettings
{
	public float[] Cosine { get; set; }
	public float[] Sine { get; set; }
	public float[] ArcCosine { get; set; }
	public float QuadrantScale { get; set; }
	public float QuadrantHigh { get; set; }
	public float QuadrantLow { get; set; }
	public float Pi { get; set; }
	public int SolverPasses { get; set; }
	public float MaximumSpeed { get; set; }
	public float MaximumAngularSpeed { get; set; }
	public float Gravity { get; set; }

	internal LimbSolverMath CreateMath()
	{
		if ( SolverPasses is < 1 or > 128 || !float.IsFinite( MaximumSpeed ) || MaximumSpeed <= 0f
			|| !float.IsFinite( MaximumAngularSpeed ) || MaximumAngularSpeed <= 0f
			|| !float.IsFinite( Gravity ) || Gravity < 0f )
			throw new ArgumentException( "Physical animation requires finite world limits and a solver pass count." );
		return new( Cosine, Sine, ArcCosine, QuadrantScale, QuadrantHigh, QuadrantLow, Pi );
	}
}
