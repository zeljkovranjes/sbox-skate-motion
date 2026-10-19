using Sandbox;
using SkateMotion.Core;
using SkateMotion.Engine;

namespace SkateMotion.Components;

/// <summary>Optional custom deck, wheels and foot targets for a skateboard on the same object.</summary>
[Title( "Skateboard Rig" ), Category( "Skate Motion" )]
public sealed class SkateboardRig : Component
{
	/// <summary>Optional imported deck model, facing +X with +Z up; uses its authored origin and scale.</summary>
	[Property, Group( "Models" )] public Model DeckModel { get; set; }

	/// <summary>Optional wheel model, axle along local +Y; used when wheel objects are unassigned.</summary>
	[Property, Group( "Models" )] public Model WheelModel { get; set; }

	/// <summary>Front left wheel object; its initial center determines the physics contact location.</summary>
	[Property, Group( "Wheel Objects" )] public GameObject FrontLeft { get; set; }

	/// <summary>Front right wheel object; assign all four objects or leave all four empty.</summary>
	[Property, Group( "Wheel Objects" )] public GameObject FrontRight { get; set; }

	/// <summary>Rear left wheel object.</summary>
	[Property, Group( "Wheel Objects" )] public GameObject RearLeft { get; set; }

	/// <summary>Rear right wheel object.</summary>
	[Property, Group( "Wheel Objects" )] public GameObject RearRight { get; set; }

	/// <summary>Optional front ankle-bone IK transform authored on the deck; kept unchanged by the rider.</summary>
	[Property, Group( "Rider" )] public GameObject FrontFoot { get; set; }

	/// <summary>Optional rear ankle-bone IK transform authored on the deck; kept unchanged by the rider.</summary>
	[Property, Group( "Rider" )] public GameObject RearFoot { get; set; }

	internal BoardRig Read( SkateSettings settings, float radius ) => new( GameObject, settings, radius,
		new[] { FrontLeft, FrontRight, RearLeft, RearRight } ) { DeckModel = DeckModel, WheelModel = WheelModel };
}
