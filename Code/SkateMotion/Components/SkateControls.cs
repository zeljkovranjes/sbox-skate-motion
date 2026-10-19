using Sandbox;
using SkateMotion.Engine;

namespace SkateMotion.Components;

/// <summary>Optional local keyboard/gamepad adapter. Bind skating actions, including SkateManual and SkateNoseManual.</summary>
public sealed class SkateControls : Component
{
	/// <summary>Board controlled by this input source.</summary>
	[Property] public Skateboard Board { get; set; }

	/// <summary>Reverse the flip-gesture interpretation for goofy stance.</summary>
	[Property] public bool Goofy { get; set; }

	private readonly LocalSkateInput _input = new();

	protected override void OnStart() => Board ??= Components.Get<Skateboard>();

	protected override void OnUpdate()
	{
		if ( !IsProxy && Board.IsValid() )
			Board.SetInput( _input.Read( Goofy, Time.Delta ) );
	}

	protected override void OnDisabled()
	{
		if ( Board.IsValid() )
			Board.SetInput( default );
	}
}
