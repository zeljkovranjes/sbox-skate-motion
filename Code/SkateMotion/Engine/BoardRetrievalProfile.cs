using Sandbox;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Physics;

namespace SkateMotion.Engine;

/// <summary>Caller-owned retrieval curves and angular math, loaded with the animation graph.</summary>
internal sealed class BoardRetrievalProfile
{
	public float[] WeightInputs { get; set; }
	public float[] WeightOutputs { get; set; }
	public float[] DurationInputs { get; set; }
	public float[] DurationOutputs { get; set; }
	public float MinimumDistance { get; set; }
	public float DistanceLimit { get; set; }
	public float MountingDistanceLimit { get; set; }
	public float MountingDurationLimit { get; set; }
	public float Step { get; set; }
	public float[] Cosine { get; set; }
	public float[] Sine { get; set; }
	public float[] ArcCosine { get; set; }
	public float[] ArcTangent { get; set; }
	public float QuadrantScale { get; set; }
	public float QuadrantHigh { get; set; }
	public float QuadrantLow { get; set; }
	public float Pi { get; set; }
	public float HalfPi { get; set; }
	public float HalfTurnThreshold { get; set; }
	public float LinearAngleThreshold { get; set; }

	internal static BoardRetrievalProfile Load( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;
		if ( !FileSystem.Data.FileExists( path ) )
			throw new InvalidOperationException( $"Board retrieval profile is missing: {path}" );
		return Json.Deserialize<BoardRetrievalProfile>( FileSystem.Data.ReadAllText( path ) )
			?? throw new InvalidOperationException( "Board retrieval profile is empty." );
	}

	internal BoardPickupPlayback CreatePlayback()
	{
		var angles = new LimbSolverMath( Cosine, Sine, ArcCosine, QuadrantScale, QuadrantHigh, QuadrantLow, Pi );
		var arc = new LimbArcTangent( ArcTangent, HalfPi, Pi );
		var blender = new LimbPoseBlender( angles, arc, HalfTurnThreshold, LinearAngleThreshold );
		var initializer = new BoardRetrievalInitializer( new( DurationInputs, DurationOutputs ), MinimumDistance,
			DistanceLimit, MountingDistanceLimit, MountingDurationLimit );
		var motion = new BoardRetrievalMotion( new( WeightInputs, WeightOutputs ), blender, Step );
		return new( initializer, motion, Step );
	}
}
