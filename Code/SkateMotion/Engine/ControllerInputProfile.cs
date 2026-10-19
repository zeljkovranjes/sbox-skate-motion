using Sandbox;
using SkateMotion.Core.Animation;
using SkateMotion.Core.Input;

namespace SkateMotion.Engine;

/// <summary>Caller-supplied numeric controller profile, loaded once from local data.</summary>
internal sealed class ControllerInputProfile
{
	public float[] ArcCoefficients { get; set; }
	public float[] Quadrants { get; set; }
	public float[] ActivityAndManual { get; set; }
	public float[] Buttons { get; set; }
	public float[] Analog { get; set; }
	public float[] ObjectFactors { get; set; }
	public float[][] CurveInputs { get; set; }
	public float[][] CurveOutputs { get; set; }
	public float[] Walking { get; set; }
	public ControllerGestureProfile Gestures { get; set; }

	internal static ControllerInputProfile Load( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;
		if ( !FileSystem.Data.FileExists( path ) )
			throw new InvalidOperationException( $"Controller profile not found in the local data folder: {path}" );
		return Json.Deserialize<ControllerInputProfile>( FileSystem.Data.ReadAllText( path ) )
			?? throw new InvalidOperationException( "Controller profile is empty." );
	}

	internal ControllerActionRouter CreateRouter()
	{
		Validate( ArcCoefficients, 8 );
		Validate( Quadrants, 2 );
		Validate( ActivityAndManual, 3 );
		Validate( Buttons, 5 );
		Validate( Analog, 14 );
		Validate( ObjectFactors, 3 );
		Validate( Walking, 2 );
		if ( CurveInputs?.Length != 3 || CurveOutputs?.Length != 3 )
			throw new ArgumentException( "Controller profile requires three directional curves." );
		var arc = new LimbArcTangent( ArcCoefficients, Quadrants[ 0 ], Quadrants[ 1 ] );
		var objects = new ObjectMovementInputProcessor( arc,
			new( CurveInputs[ 0 ], CurveOutputs[ 0 ] ), new( CurveInputs[ 1 ], CurveOutputs[ 1 ] ),
			new( CurveInputs[ 2 ], CurveOutputs[ 2 ] ), ObjectFactors[ 0 ], ObjectFactors[ 1 ], ObjectFactors[ 2 ] );
		var buttons = new RidingButtonTuning( Buttons[ 0 ], Buttons[ 1 ], Buttons[ 2 ], Buttons[ 3 ], Buttons[ 4 ] );
		var analog = new RidingAnalogTuning( Analog[ 0 ], Analog[ 1 ], Analog[ 2 ], Analog[ 3 ],
			Analog[ 4 ], Analog[ 5 ], Analog[ 6 ], Analog[ 7 ], Analog[ 8 ], Analog[ 9 ],
			Analog[ 10 ], Analog[ 11 ], Analog[ 12 ], Analog[ 13 ] );
		return new( arc, objects, new( ActivityAndManual[ 0 ], ActivityAndManual[ 1 ],
			ActivityAndManual[ 2 ], buttons, analog ), 128 );
	}

	private static void Validate( float[] values, int count )
	{
		if ( values?.Length != count )
			throw new ArgumentException( "Controller profile has an invalid numeric field count." );
		foreach ( var value in values )
		{
			if ( !float.IsFinite( value ) )
				throw new ArgumentException( "Controller profile values must be finite." );
		}
	}
}
