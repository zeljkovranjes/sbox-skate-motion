using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.BoardModel;
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

	/// <summary>
	/// Take the skateboard's Board Model apart by its geometry even where it has bones for its wheels: for a model whose
	/// bones do not move its parts.
	/// </summary>
	[Property, Group( "Board Model Bones" )] public bool IgnoreBones { get; set; }

	/// <summary>Bone of the Board Model moving its front truck; empty finds it by name and place.</summary>
	[Property, Group( "Board Model Bones" )] public string FrontTruckBone { get; set; }

	/// <summary>Bone moving the rear truck; empty finds it by name and place.</summary>
	[Property, Group( "Board Model Bones" )] public string RearTruckBone { get; set; }

	/// <summary>Bone moving the front left wheel; with all four wheel bones named the model is moved by its bones.</summary>
	[Property, Group( "Board Model Bones" )] public string FrontLeftWheelBone { get; set; }

	/// <summary>Bone moving the front right wheel.</summary>
	[Property, Group( "Board Model Bones" )] public string FrontRightWheelBone { get; set; }

	/// <summary>Bone moving the rear left wheel.</summary>
	[Property, Group( "Board Model Bones" )] public string RearLeftWheelBone { get; set; }

	/// <summary>Bone moving the rear right wheel.</summary>
	[Property, Group( "Board Model Bones" )] public string RearRightWheelBone { get; set; }

	/// <param name="parts">The skateboard's Board Model taken apart, or null.</param>
	/// <param name="whole">A board model whose parts could not be found, shown whole in place of the deck.</param>
	internal BoardRig Read( SkateSettings settings, float radius, BoardModelParts parts = null, Model whole = null ) =>
		new( GameObject, settings, radius, new[] { FrontLeft, FrontRight, RearLeft, RearRight }, parts, Bones( parts ) )
		{
			DeckModel = whole ?? (parts is null ? DeckModel : null),
			WheelModel = parts is null ? WheelModel : null
		};

	// Which bone moves each part of the board model: the ones found, without them where bones are ignored, with any named here.
	private int[] Bones( BoardModelParts parts )
	{
		if ( parts is null )
			return null;
		var bones = BoardLayout.NoBones();
		for ( var part = 0; part < bones.Length && !IgnoreBones; part++ )
			bones[ part ] = parts.Layout.PartBones[ part ];
		var names = new[] { FrontTruckBone, RearTruckBone, FrontLeftWheelBone, FrontRightWheelBone, RearLeftWheelBone, RearRightWheelBone };
		for ( var i = 0; i < names.Length; i++ )
		{
			if ( !string.IsNullOrWhiteSpace( names[ i ] ) && parts.Source.Bones.GetBone( names[ i ] ) is { } bone )
				bones[ (int)BoardPart.FrontTruck + i ] = bone.Index;
		}
		return bones;
	}
}
