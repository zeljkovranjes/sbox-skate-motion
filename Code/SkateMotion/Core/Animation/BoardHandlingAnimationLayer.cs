using System.Collections.Generic;

namespace SkateMotion.Core.Animation;

/// <summary>Connects handling phases, retained motion trees and a masked overlay to an already sampled locomotion pose.</summary>
internal sealed class BoardHandlingAnimationLayer : IAnimationChannelTreeFactory
{
	private readonly BoardHandlingController _controller = new();
	private readonly BoardHandlingAnimationTree[,] _trees = new BoardHandlingAnimationTree[ 9, 3 ];
	private readonly AnimationChannelBlend _blend = new( default );
	private readonly AnimationChannelPoseTreeOwner _owner;
	private readonly AnimationOwnerPoseContext _context;
	private float _yaw;

	internal BoardHandlingAnimationLayer( RawAnimationClipSet clips, AnimationGraphDefinition graph,
		AnimationOwnerPoseContext context = null )
	{
		ArgumentNullException.ThrowIfNull( clips );
		ArgumentNullException.ThrowIfNull( graph.BoardHandling );
		_context = context ?? new( new AnimationPoseSamplingContext( 1, false ), 1, true, 0f );
		var key = AnimationAttributeKey.Encode( graph.BoardHandling.YawAttribute );
		BoardHandlingAnimationTree first = null;
		foreach ( var pair in graph.BoardHandling.Motions )
		{
			if ( !Enum.TryParse<BoardHandlingMotion>( pair.Key, out var motion ) || motion == BoardHandlingMotion.None
				|| (int)motion >= _trees.GetLength( 0 ) )
				throw new ArgumentException( "Unknown board handling motion." );
			var leaves = ResolveLeaves( clips, graph.Blends, pair.Value );
			var reference = clips.Reference( leaves[ 0 ].ReferenceIndex ).ToArray();
			for ( var slot = 0; slot < _trees.GetLength( 1 ); slot++ )
			{
				var tree = new BoardHandlingAnimationTree( leaves, reference, key, _context );
				_trees[ (int)motion, slot ] = tree;
				first ??= tree;
			}
		}
		if ( first is null )
			throw new ArgumentException( "Board handling needs motion definitions." );
		for ( var motion = BoardHandlingMotion.FrontInto; motion <= BoardHandlingMotion.BackOut; motion++ )
			if ( _trees[ (int)motion, 0 ] is null )
				throw new ArgumentException( "Retrieval needs all front and back phases." );
		first.InUse = true;
		_owner = new AnimationChannelPoseTreeOwner( first, this, _blend );
	}

	internal bool Active { get; private set; }
	internal float Weight => Active ? _blend.State.Weight : 0f;
	internal BoardHandlingPhase Phase => _controller.Phase;
	internal BoardHandlingMotion Motion { get; private set; }
	internal BoardHandlingResult Result { get; private set; }

	internal void Update( in BoardHandlingInput input, float delta, SkeletonPose basePose )
	{
		var playback = new BoardHandlingPlayback( Active, _owner.Duration - _owner.Elapsed, _owner.Elapsed );
		var availableInput = input with
		{
			DropRequested = input.DropRequested && _trees[ (int)BoardHandlingMotion.Drop, 0 ] is not null,
			ThrowRequested = input.ThrowRequested && _trees[ (int)BoardHandlingMotion.Throw, 0 ] is not null
		};
		Result = _controller.Update( availableInput, playback );
		if ( Result.Command == BoardHandlingCommand.Stop )
			_blend.Stop( Result.BlendSeconds, false );
		else if ( Result.Command is BoardHandlingCommand.Transition or BoardHandlingCommand.Sequence )
			Play( Result );
		if ( Result.PublishOrientation )
		{
			_yaw = Result.YawDegrees;
			foreach ( var tree in _trees )
				if ( tree is not null && tree.InUse )
					tree.UpdateYaw( _yaw );
		}
		if ( !Active )
			return;
		if ( _blend.Update( delta, _owner ) )
		{
			var completion = default(AnimationPlaybackCompletion);
			_owner.Advance( delta, 0f, ref completion );
			_blend.CompleteOverlayUpdate( completion.Finished );
		}
		foreach ( var tree in _trees )
			if ( tree is not null && tree.InUse )
				tree.UpdateSamplingContext();
		if ( _owner.Prepare( _blend.State.Weight > 0f, _context.ConsumeHistory, _context.SamplingFlags )
			&& _blend.State.Weight > 0f )
			basePose.BlendWeightedToward( _owner.Pose, _blend.State.Weight, false );
		_owner.CleanupCompleted();
		if ( _blend.Finished )
			Active = false;
	}

	private void Play( BoardHandlingResult result )
	{
		var held = result.Motion is BoardHandlingMotion.FrontCycle or BoardHandlingMotion.BackCycle
			or BoardHandlingMotion.FrontOut or BoardHandlingMotion.BackOut;
		var request = new AnimationChannelReplacement( (int)result.Motion, 1f, true,
			result.Command == BoardHandlingCommand.Transition, result.BlendSeconds,
			0.25f, 0.25f, held ? 0xc2000000u : 0x02000000u, true );
		if ( !Active )
		{
			var tree = Create( request );
			_owner.Restart( tree );
			_blend.StartEnvelope( result.BlendSeconds, request.ExitDuration, request.Flags );
			Active = true;
		}
		else if ( _owner.Replace( request ) != AnimationChannelReplaceResult.Success )
		{
			return;
		}
		Motion = result.Motion;
	}

	/// <summary>Leases prebuilt storage so gameplay transitions do not allocate new trees.</summary>
	public IAnimationPlaybackTree Create( AnimationChannelReplacement request )
	{
		for ( var slot = 0; slot < _trees.GetLength( 1 ); slot++ )
		{
			var tree = _trees[ request.Definition, slot ];
			if ( tree is null || tree.InUse )
				continue;
			tree.InUse = true;
			tree.Restart( request.Rate );
			return tree;
		}
		throw new InvalidOperationException( "No free handling tree for this motion." );
	}

	/// <summary>Tree storage is already reserved before publication.</summary>
	public void Commit( IAnimationPlaybackTree tree ) { }

	/// <summary>Releases storage only after its successor has been published.</summary>
	public void Release( IAnimationPlaybackTree tree ) => ((BoardHandlingAnimationTree)tree).InUse = false;

	private static RawAnimationClip[] ResolveLeaves( RawAnimationClipSet clips,
		Dictionary<string, BlendDefinition> blends, string name )
	{
		var direct = clips.Find( name );
		if ( direct is not null )
			return new[] { direct };
		if ( !blends.TryGetValue( name, out var definition ) || definition.Type != "blend"
			|| definition.Children.Count < 2 || definition.Children.Count > 5 )
			throw new ArgumentException( "Handling motions need keyed clips or direct-leaf directional blends." );
		var leaves = new RawAnimationClip[ definition.Children.Count ];
		for ( var child = 0; child < leaves.Length; child++ )
		{
			leaves[ child ] = clips.Find( definition.Children[ child ].Name )
				?? throw new ArgumentException( "Handling blend children must be keyed clips." );
			if ( leaves[ child ].Attributes is null || leaves[ child ].ReferenceIndex != leaves[ 0 ].ReferenceIndex )
				throw new ArgumentException( "Handling children need attributes and a shared reference pose." );
		}
		return leaves;
	}
}
