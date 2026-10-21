using System.Linq;

namespace SkateMotion.Core.Animation;

using Quaternion = System.Numerics.Quaternion;

/// <summary>
/// Keeps a rider alive over its clips, so a held pose (a carve, a stop on the board, a slide held in one key) never looks
/// frozen. It breathes while it rides with its hands free, on the ground or on a rail: at a resting pace, and quicker
/// and deeper for a few seconds after a push, a landing or a bail. On a rail it also balances: a small sway of the torso
/// and arms (see <see cref="GrindBalance"/>), as much as the grind's own clip holds the body still. Both ease out in the
/// air off a rail, on a wall, in a grab, a hand plant or a skitch, off the board, and whenever the hands are wanted for
/// the board (holding it, or a board-handling motion), so neither moves a hand off a grip. They only turn the spine,
/// neck, head, shoulders and upper arms (see <see cref="BreathingLayer"/> and <see cref="BalanceLayer"/>), so the feet
/// and the board stay where the clips and the foot fits put them.
/// </summary>
internal sealed class RiderBreathing
{
	/// <summary>Exertion (0 to 1) a push winds the rider to as it starts.</summary>
	internal const float PushEffort = 0.35f;

	/// <summary>Exertion each further second of pushing adds, up to <see cref="LongPush"/>.</summary>
	internal const float PushBuild = 0.15f;

	/// <summary>Most exertion pushing alone winds the rider to.</summary>
	internal const float LongPush = 0.8f;

	/// <summary>Exertion after the lightest landing, and after one coming down at <see cref="HeavyImpact"/> or harder.</summary>
	internal const float LightLanding = 0.3f;

	internal const float HeavyLanding = 0.9f;

	/// <summary>Speed (metres per second) the board comes down at in a heavy landing.</summary>
	internal const float HeavyImpact = 6f;

	private readonly BreathingLayer _layer;
	private readonly BalanceLayer _balanceLayer;
	private readonly int[] _spine;
	private float _pushTime;
	private bool _airborne;
	private float _fall;
	private float _delta;

	internal RiderBreathing( ClipSkeleton skeleton )
	{
		_layer = new BreathingLayer( skeleton );
		_balanceLayer = new BalanceLayer( skeleton );
		_spine = HumanoidBones.Spine.Select( skeleton.IndexOf ).Where( bone => bone >= 0 ).ToArray();
	}

	/// <summary>The breath's timing: phase, pace, depth and how much of it shows.</summary>
	internal BreathingRhythm Rhythm { get; } = new();

	/// <summary>The balance sway's timing on a rail, and how still the grind's own clip holds the body.</summary>
	internal GrindBalance Balance { get; } = new();

	/// <summary>Whether the breath shows on the ground for this input: riding there with the hands free.</summary>
	/// <param name="handsBusy">Whether the hands are wanted for the board now, e.g. by a board-handling motion.</param>
	internal static bool Shown( in RiderAnimationInput input, bool handsBusy ) =>
		input.Activity == RiderActivity.Riding && !input.Airborne && !input.OffBoard && !input.WallRiding
		&& !input.HoldingBoard && !input.Skitching && !handsBusy && string.IsNullOrEmpty( input.Grind )
		&& string.IsNullOrEmpty( input.Grab ) && string.IsNullOrEmpty( input.Handplant );

	/// <summary>
	/// Whether the rider grinds or slides a rail with its hands free (no grab, hand plant or skitch): it breathes there and
	/// balances.
	/// </summary>
	/// <param name="handsBusy">Whether the hands are wanted for the board now, e.g. by a board-handling motion.</param>
	internal static bool OnRail( in RiderAnimationInput input, bool handsBusy ) =>
		input.Activity == RiderActivity.Riding && !string.IsNullOrEmpty( input.Grind ) && !input.OffBoard
		&& !input.HoldingBoard && !input.Skitching && !handsBusy && string.IsNullOrEmpty( input.Grab )
		&& string.IsNullOrEmpty( input.Handplant );

	/// <summary>Winds the rider for what it is doing and runs the breath and the balance on by <paramref name="delta"/> seconds.</summary>
	/// <param name="handsBusy">Whether the hands are wanted for the board now, e.g. by a board-handling motion.</param>
	internal void Update( in RiderAnimationInput input, bool handsBusy, float delta )
	{
		if ( !(delta > 0f) || !float.IsFinite( delta ) )
			return;
		_delta = delta;
		var riding = input.Activity == RiderActivity.Riding;
		// A push winds the rider as it starts, and more the longer it goes on.
		_pushTime = riding && input.Pushing ? _pushTime + delta : 0f;
		if ( _pushTime > 0f )
			Rhythm.Exert( Math.Min( LongPush, PushEffort + PushBuild * _pushTime ) );
		// A landing winds it by how hard the board came down: the fall speed is kept through the air, so a landing that
		// first touches with one wheel still counts as the drop it was.
		if ( riding && input.Airborne )
		{
			_fall = MathF.Max( _fall, -input.VerticalSpeed );
			_airborne = true;
		}
		else if ( _airborne )
		{
			if ( riding )
			{
				var impact = Math.Clamp( MathF.Max( input.LandingSpeed, _fall ) / HeavyImpact, 0f, 1f );
				Rhythm.Exert( LightLanding + (HeavyLanding - LightLanding) * impact );
			}
			_airborne = false;
			_fall = 0f;
		}
		// A bail winds it fully, and it is still winded for a while after getting back up.
		if ( input.Activity == RiderActivity.Bailed )
			Rhythm.Exert( 1f );
		var onRail = OnRail( input, handsBusy );
		Rhythm.Update( delta, onRail || Shown( input, handsBusy ) );
		Balance.Update( delta, onRail );
	}

	/// <summary>
	/// Lays the breath, and on a rail the balance, over a pose of the clip skeleton (local transforms, changed in place).
	/// The clip's own torso is watched first, before either turns it, to judge how still the clip holds the body.
	/// </summary>
	internal void Apply( SkeletonPose pose )
	{
		Balance.Watch( Torso( pose ), _delta );
		_layer.Apply( pose, Rhythm.Inhale );
		var weight = Balance.Weight;
		if ( weight > 0f )
			_balanceLayer.Apply( pose, Balance.Pitch * weight, Balance.Roll * weight );
	}

	// The spine's turn from the hips up to the chest, as the pose has it: its bones' local turns, outermost last.
	private Quaternion Torso( SkeletonPose pose )
	{
		var torso = Quaternion.Identity;
		foreach ( var bone in _spine )
			torso = Quaternion.Normalize( torso * pose.Rotations[ bone ] );
		return torso;
	}
}
