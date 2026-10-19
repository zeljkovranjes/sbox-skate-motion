using Sandbox;
using SkateMotion.Engine;

namespace SkateMotion.Components;

/// <summary>Draws the simulated parts, contacts and state of a skateboard while the game runs.</summary>
[Title( "Skate Debugger" ), Category( "Skate Motion" ), Icon( "bug_report" )]
public sealed class SkateDebugger : Component
{
	/// <summary>Board to inspect; empty uses a Skateboard on this object.</summary>
	[Property] public Skateboard Board { get; set; }

	/// <summary>Draw wheel, truck and deck positions.</summary>
	[Property] public bool ShowBodies { get; set; } = true;

	/// <summary>Draw contact points and normals; green for wheels, orange for the deck, cyan for trucks on rails.</summary>
	[Property] public bool ShowContacts { get; set; } = true;

	/// <summary>Draw the deck velocity vector, scaled to one inch per inch per second times this factor.</summary>
	[Property, Range( 0f, 1f )] public float VelocityScale { get; set; } = 0.1f;

	/// <summary>Print speed, mode, grind and energy above the board.</summary>
	[Property] public bool ShowText { get; set; } = true;

	protected override void OnStart() => Board ??= Components.Get<Skateboard>();

	protected override void OnUpdate()
	{
		if ( !Board.IsValid() )
			return;
		// Debug-only: the snapshot allocates, which is acceptable while inspecting a board.
		var telemetry = Board.CaptureTelemetry();
		if ( telemetry is null )
			return;
		if ( ShowBodies )
			DrawBodies( telemetry );
		if ( ShowContacts )
			DrawContacts( telemetry );
		var deck = Board.WorldPosition;
		if ( VelocityScale > 0f )
			DebugOverlay.Line( deck, deck + Board.Body.Velocity * VelocityScale, Color.Yellow );
		if ( ShowText )
			DebugOverlay.Text( deck + Vector3.Up * 24f, Describe( telemetry ), 14f, TextFlag.Center, Color.White );
	}

	private void DrawBodies( SkateTelemetry telemetry )
	{
		foreach ( var body in telemetry.Bodies )
		{
			var position = body.Position * SkateUnits.InchesPerMetre;
			var radius = body.Part == SkateBodyPart.Deck ? 1.5f : 1.12f;
			DebugOverlay.Sphere( new Sphere( position, radius ), body.Part == SkateBodyPart.Deck ? Color.White : Color.Gray );
		}
	}

	private void DrawContacts( SkateTelemetry telemetry )
	{
		foreach ( var contact in telemetry.Contacts )
		{
			var point = contact.SurfacePoint * SkateUnits.InchesPerMetre;
			var color = contact.Part switch
			{
				SkateBodyPart.Deck => Color.Orange,
				SkateBodyPart.FrontTruck or SkateBodyPart.RearTruck => Color.Cyan,
				_ => Color.Green
			};
			DebugOverlay.Line( point, point + contact.Normal * 6f, color );
			DebugOverlay.Sphere( new Sphere( point, 0.4f ), color );
		}
	}

	private string Describe( SkateTelemetry telemetry )
	{
		var state = Board.State;
		var mode = state.IsBailed ? "bailed" : state.Grind != SkateGrind.None ? state.Grind.ToString()
			: state.Balance != SkateBalanceMode.None ? state.Balance.ToString()
			: state.IsAirborne ? "airborne" : "rolling";
		return $"{state.Speed:0.00} m/s  {mode}  wheels {state.GroundedWheels}\n"
			+ $"contacts {telemetry.Contacts.Length}  energy {telemetry.KineticEnergy:0.0} J  joint {telemetry.MaximumJointError * 1000f:0.0} mm";
	}
}
