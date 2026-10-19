// Plain-.NET cost of the Core simulation: no scene queries, no rendering. Run with -c Release.
using System.Diagnostics;
using System.Numerics;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;
using SkateMotion.Core.Grinding;
using SkateMotion.Core.Vehicle;

const float radius = 0.0285f;
const float step = 1f / 240f;
const int warmup = 240;
const int measured = 240 * 10;

foreach ( var scenario in new[] { "rolling", "grinding" } )
{
	var settings = new SkateSettings();
	var origins = new Vector3[ 4 ];
	for ( var i = 0; i < 4; i++ )
		origins[ i ] = SkateWheels.Offset( i, settings ) - Vector3.UnitZ * (settings.RideHeight - radius);
	var board = new ArticulatedBoard( settings, origins, radius );
	var pilot = new BoardPilot( settings );
	var grinding = scenario == "grinding";
	var railHeight = 0.4f;
	var height = grinding ? railHeight + RailContacts.AxleRadius + settings.RideHeight - radius : settings.RideHeight;
	board.Reset( Vector3.UnitZ * height, Quaternion.Identity, Vector3.UnitX * 4f, Vector3.Zero );
	board.Body( 7 ) = new DynamicBody { Rotation = Quaternion.Identity };
	var rails = new[] { new RailSegment( new Vector3( -1f, 0f, railHeight ), new Vector3( 1000f, 0f, railHeight ) ) };
	var contacts = new CollisionContact[ ArticulatedBoard.ContactCapacity ];
	var timer = new Stopwatch();
	long allocated = 0;
	for ( var tick = 0; tick < warmup + measured; tick++ )
	{
		if ( tick == warmup )
		{
			allocated = GC.GetAllocatedBytesForCurrentThread();
			timer.Start();
		}
		board.BeginStep();
		var count = 0;
		var grind = default( GrindLock );
		if ( grinding )
		{
			grind = RailContacts.Build( board, rails, 7, settings.GrindFriction, contacts, ref count );
		}
		else
		{
			for ( var wheel = 0; wheel < 4; wheel++ )
			{
				ref var body = ref board.Body( wheel + 3 );
				var point = body.Position - Vector3.UnitZ * radius;
				var axle = Vector3.Transform( Vector3.UnitY, body.Rotation );
				var tangent = Vector3.Normalize( Vector3.Cross( axle, Vector3.UnitZ ) );
				contacts[ count++ ] = new( wheel + 3, 7, point, new Vector3( point.X, point.Y, 0f ), Vector3.UnitZ,
					tangent, Vector3.Cross( Vector3.UnitZ, tangent ), 0.68f, 0.544f, 0f );
			}
		}
		var span = contacts.AsSpan( 0, count );
		pilot.Apply( board, span, new ControlInput( Push: 1f ), step, grind );
		board.Step( span, 0f, 0f, step );
	}
	timer.Stop();
	allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
	var perSubstep = timer.Elapsed.TotalMilliseconds / measured;
	Console.WriteLine( $"{scenario}: {perSubstep * 1000:0.0} us per 240 Hz substep, "
		+ $"{perSubstep * 240:0.00} ms per simulated second, {perSubstep * 4:0.000} ms per 60 Hz tick, "
		+ $"{allocated / (double)measured:0.0} bytes allocated per substep" );
}
