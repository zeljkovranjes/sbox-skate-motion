using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects graph play requests to retained base trees and the preceding frame's completion.</summary>
internal sealed class StateGraphTreePlayer
{
	private readonly AnimationBasePoseTreeOwner _owner;
	private readonly Dictionary<string, int> _motions = new( StringComparer.OrdinalIgnoreCase );

	internal StateGraphTreePlayer( StateGraphDefinition definition, IAnimationBasePoseTreeFactory factory,
		IReadOnlyList<string> motions )
	{
		ArgumentNullException.ThrowIfNull( motions );
		_owner = new AnimationBasePoseTreeOwner( factory );
		Graph = new StateGraph( definition, true, Play );
		for ( var i = 0; i < motions.Count; i++ )
			_motions.Add( motions[ i ], i );
	}

	internal StateGraph Graph { get; }
	internal string ClipName { get; private set; }
	internal AnimationBasePoseTreeOwner Owner => _owner;
	internal AnimationPlaybackCompletion Completion => _owner.Completion;
	internal AnimationPlaybackStatus Status => new(
		Completion.Remaining, _owner.RetainedCount != 0, Completion.Finished );

	/// <summary>Evaluates requests, prepares the current pose, then cleans and advances retained clocks.</summary>
	internal bool Update( StateFacts facts, float delta, float phase, SkeletonPose output,
		bool consumeHistory, uint samplingFlags )
	{
		Graph.ClipLength = _owner.Root?.Duration ?? float.PositiveInfinity;
		Graph.Update( facts, delta, Status );

		var prepared = _owner.Root is not null && _owner.Prepare( true, consumeHistory, samplingFlags );
		if ( prepared )
			output.CopyFrom( _owner.Pose );
		// Body preparation precedes this tick's cleanup and advance. Graph expiry above
		// reads the retained completion, including sequences waiting behind a previous motion.
		_owner.AdvanceFrame( delta, phase );
		return prepared;
	}

	private void Play( StatePlayDefinition play, string motion )
	{
		if ( !_motions.TryGetValue( motion, out var definition ) )
			throw new InvalidOperationException( $"No motion definition is bound for {motion}." );
		var transition = play.Transition switch
		{
			"play" => AnimationBaseTransition.Play,
			"blend" => AnimationBaseTransition.Blend,
			"channelblend" => AnimationBaseTransition.ChannelBlend,
			"sequence" => AnimationBaseTransition.Sequence,
			_ => throw new NotSupportedException( $"Unsupported base transition: {play.Transition ?? "unspecified"}." )
		};
		if ( !_owner.Replace( new( definition, transition, play.Speed, play.BlendTime,
			play.Synchronization, play.TransitionUnder,
			UseChannelFromWeights: play.UseChannelFromWeights ) ) )
			throw new InvalidOperationException( $"Could not construct the requested motion {motion}." );
		ClipName = motion;
	}
}
