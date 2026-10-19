using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

/// <summary>
/// Runs a <see cref="StateGraphDefinition"/>. Each frame, in order: the current states' transitions (deepest
/// state first, higher priority first) may jump elsewhere when their own tests and the target's entry tests pass; otherwise a state whose staying tests fail hands
/// control back to its parent, which picks again; otherwise an interruptible state yields to an earlier
/// sibling with entry tests of its own that may now be entered (any earlier sibling under the priority policy). A state may only be entered when its entry tests pass and, if it has children,
/// one of its active children may be entered too (so a path always ends in a state without children).
/// Entering a state then enters the first such child, and so on down. The deepest entered state with a clip sets what plays.
/// </summary>
internal sealed class StateGraph
{
	private const int Entry = 1;
	private const int Staying = 2;

	private sealed class Node
	{
		internal StateGraphDefinition Definition;
		internal Node Parent;
		internal Node[] Children;
		internal int Index;
		internal int Depth;
		internal float Entered;
		internal (StateTransitionDefinition Transition, Node Target)[] Transitions;
	}

	private readonly Node _root;
	private readonly List<Node> _path = new();
	private readonly Random _random = new( 7 );
	private readonly Action<StatePlayDefinition, string> _playRequested;
	private StateFacts _facts;
	private AnimationPlaybackStatus? _playback;
	private float _time;
	private float _playStarted;
	private string _previous = "";
	private bool _entered;

	internal StateGraph( StateGraphDefinition definition, bool deferInitialEntry = false,
		Action<StatePlayDefinition, string> playRequested = null )
	{
		_playRequested = playRequested;
		_root = Build( definition, null, 0, 0 );
		Resolve( _root );
		_entered = !deferInitialEntry;
		if ( _entered )
			Enter( _root );
	}

	/// <summary>Dotted names of the current states below the root.</summary>
	internal string State => string.Join( ".", _path.Skip( 1 ).Select( node => node.Definition.Name ) );

	/// <summary>True when any current state lists <paramref name="action"/> among its actions.</summary>
	internal bool HasAction( string action )
	{
		foreach ( var node in _path )
		{
			foreach ( var listed in node.Definition.Actions )
			{
				if ( string.Equals( listed, action, StringComparison.OrdinalIgnoreCase ) )
					return true;
			}
		}
		return false;
	}

	/// <summary>Dotted names of the states current before the last change.</summary>
	internal string PreviousState => _previous;

	/// <summary>The clip or blend to play now (after mirror, switch and boardless choices), or null for none yet.</summary>
	internal string Clip { get; private set; }

	internal StatePlayDefinition Play { get; private set; }

	/// <summary>Counts every new play request, so a restarted clip can be told from one that keeps playing.</summary>
	internal int PlayCount { get; private set; }

	/// <summary>Seconds since the current clip was asked for, at its play speed.</summary>
	internal float PlayTime => (_time - _playStarted) * (Play?.Speed ?? 1f);

	/// <summary>Length of the current clip in seconds, set by the player before each update.</summary>
	internal float ClipLength { get; set; } = float.PositiveInfinity;

	internal void Update( StateFacts facts, float delta, AnimationPlaybackStatus? playback = null )
	{
		_facts = facts;
		_playback = playback;
		_time += delta;
		if ( !_entered )
		{
			_entered = true;
			Enter( _root );
			return;
		}
		for ( var i = _path.Count - 1; i >= 0; i-- )
		{
			foreach ( var (transition, target) in _path[ i ].Transitions )
			{
				if ( target is null || !PassesTransition( transition.Guard, _path[ i ] ) || !CanEnter( target ) )
					continue;
				Enter( target );
				return;
			}
		}
		for ( var i = 1; i < _path.Count; i++ )
		{
			if ( Passes( _path[ i ].Definition.Guard, Staying, _path[ i ] ) )
				continue;
			Choose( _path[ i - 1 ], null );
			return;
		}
		// A current state that may not be interrupted keeps its whole path while that path's staying tests pass:
		// nothing earlier anywhere above it takes over.
		if ( string.Equals( _path[ ^1 ].Definition.Interruptible, "false", StringComparison.OrdinalIgnoreCase ) )
			return;
		for ( var i = 1; i < _path.Count; i++ )
		{
			var node = _path[ i ];
			if ( !Interruptible( node, i ) )
				continue;
			var priority = string.Equals( node.Parent.Definition.Policy, "priority", StringComparison.OrdinalIgnoreCase );
			foreach ( var sibling in node.Parent.Children )
			{
				if ( sibling == node )
					break;
				// Unguarded states are defaults: they never take over, except under the priority policy.
				if ( sibling.Definition.Active && (priority || sibling.Definition.Guard is not null) && Reaches( sibling ) )
				{
					Enter( sibling );
					return;
				}
			}
		}
	}

	// A transition only lands where the target, and every state above it that is not already current, may be
	// entered, and where the target leads down to a state without children.
	private bool CanEnter( Node target )
	{
		if ( !Reaches( target ) )
			return false;
		for ( var node = target.Parent; node is not null && !_path.Contains( node ); node = node.Parent )
		{
			if ( !Passes( node.Definition.Guard, Entry, node ) )
				return false;
		}
		return true;
	}

	// A state may be entered when its entry tests pass and, if it has children, one of its active children may be.
	private bool Reaches( Node node )
	{
		if ( !Passes( node.Definition.Guard, Entry, node ) )
			return false;
		if ( node.Children.Length == 0 )
			return true;
		foreach ( var child in node.Children )
		{
			if ( child.Definition.Active && Reaches( child ) )
				return true;
		}
		return false;
	}

	private bool Interruptible( Node node, int depth )
	{
		var rule = node.Definition.Interruptible ?? "true";
		if ( rule.Equals( "true", StringComparison.OrdinalIgnoreCase ) )
			return true;
		if ( rule.Equals( "false", StringComparison.OrdinalIgnoreCase ) )
			return false;
		return depth + 1 < _path.Count && _path[ depth + 1 ].Definition.Name.Equals( rule, StringComparison.OrdinalIgnoreCase );
	}

	// Makes the path end at node, then enters its children.
	private void Enter( Node node )
	{
		_previous = State;
		var chain = new List<Node>();
		for ( var at = node; at is not null; at = at.Parent )
			chain.Add( at );
		chain.Reverse();
		var keep = 0;
		while ( keep < _path.Count && keep < chain.Count && _path[ keep ] == chain[ keep ] )
			keep++;
		_path.RemoveRange( keep, _path.Count - keep );
		for ( var i = keep; i < chain.Count; i++ )
			Push( chain[ i ] );
		// Re-entering a state already on the path restarts it.
		if ( keep == chain.Count )
			Restart( node );
		Choose( node, node );
	}

	private void Restart( Node node )
	{
		node.Entered = _time;
		if ( node.Definition.Play is not null )
			Start( node.Definition.Play );
	}

	// Drops everything below parent and enters the first child that may be entered.
	private void Choose( Node parent, Node entered, bool record = true )
	{
		if ( parent != entered && record )
			_previous = State;
		var at = _path.IndexOf( parent );
		_path.RemoveRange( at + 1, _path.Count - at - 1 );
		for ( var node = parent; node.Children.Length > 0; )
		{
			var next = node.Children.FirstOrDefault( child => child.Definition.Active && Reaches( child ) );
			if ( next is null )
			{
				// Nothing below can run any more: leave this state too and let its parent choose.
				if ( node.Parent is not null && node == parent )
					Choose( node.Parent, null, false );
				break;
			}
			Push( next );
			node = next;
		}
	}

	private void Push( Node node )
	{
		_path.Add( node );
		node.Entered = _time;
		if ( node.Definition.Play is not null )
			Start( node.Definition.Play );
	}

	private void Start( StatePlayDefinition play )
	{
		var facts = _facts;
		var clip = play.Clip;
		if ( facts is not null )
		{
			if ( facts.Switch && !string.IsNullOrEmpty( play.SwitchClip ) )
				clip = play.SwitchClip;
			if ( facts.Mirrored && !string.IsNullOrEmpty( play.MirrorClip ) )
				clip = play.MirrorClip;
			if ( facts.NoBoard && !string.IsNullOrEmpty( play.NoBoardClip ) )
				clip = play.NoBoardClip;
		}
		Play = play;
		Clip = clip;
		PlayCount++;
		_playStarted = _time;
		ClipLength = float.PositiveInfinity;
		// The playback reported for this update was the previous clip's; the new clip has not ended.
		_playback = null;
		_playRequested?.Invoke( play, clip );
	}

	// Null when no test in the expression applies to this phase.
	private bool? Evaluate( StateTestDefinition test, int phase, Node node )
	{
		if ( test is null )
			return null;
		if ( string.IsNullOrEmpty( test.Test ) )
		{
			var op = (test.Op ?? "and").ToLowerInvariant();
			bool? result = null;
			foreach ( var item in test.Items )
			{
				var value = Evaluate( item, phase, node );
				if ( value is null )
					continue;
				result = op == "or" ? (result ?? false) || value.Value : (result ?? true) && value.Value;
			}
			return op == "not" && result is not null ? !result.Value : result;
		}
		if ( (test.Mask & phase) == 0 )
			return null;
		return Check( test, node );
	}

	private bool Passes( StateTestDefinition test, int phase, Node node ) => Evaluate( test, phase, node ) ?? true;

	// A transition's guard reads "time-in-parent" as the time spent in the state that owns the transition (a landing
	// hands over to a push only once it has played a moment), where a state's own guard reads its parent's time.
	private bool PassesTransition( StateTestDefinition test, Node owner )
	{
		_transitionOwner = owner;
		try
		{
			return Passes( test, ~0, owner );
		}
		finally
		{
			_transitionOwner = null;
		}
	}

	private Node _transitionOwner;

	private bool Check( StateTestDefinition test, Node node )
	{
		var facts = _facts ?? new StateFacts();
		switch ( test.Test )
		{
			case "clip-ending":
				if ( _playback is { } playback )
					return playback.ExpiresWithin( test.Within, test.Wait );
				if ( test.Wait && Play is not null && PlayTime < Play.BlendTime )
					return false;
				return ClipLength - PlayTime <= test.Within;
			case "intent":
				return facts.Intents.TryGetValue( test.Intent ?? "", out var intent ) && Compare( test, test.Absolute ? MathF.Abs( intent ) : intent );
			case "grind":
				return string.Equals( facts.Grind, test.Grind, StringComparison.OrdinalIgnoreCase );
			case "physics":
				return string.Equals( facts.Physics, test.State, StringComparison.OrdinalIgnoreCase );
			case "in-state":
				return Contains( State, test.State );
			case "previous-state":
				return Contains( _previous, test.State );
			case "time-in-state":
			{
				var named = _path.LastOrDefault( n => n.Definition.Name.Equals( test.State, StringComparison.OrdinalIgnoreCase ) );
				return named is not null && Compare( test, _time - named.Entered );
			}
			case "time-in-parent":
				return (_transitionOwner ?? node?.Parent) is { } timed && Compare( test, _time - timed.Entered );
			case "speed":
				return Compare( test, test.Absolute ? MathF.Abs( facts.Speed ) : facts.Speed );
			case "clip-flag":
				return facts.Facts.TryGetValue( "clip-flag:" + test.Flag, out var flag ) && flag != 0f;
			case "random":
				return _random.Next( 2 ) == 0;
			default:
				if ( !facts.Facts.TryGetValue( test.Fact ?? "", out var fact ) || !facts.MatchesArguments( test ) )
					return false;
				return test.Compare.Count > 0 ? Compare( test, fact ) : fact != 0f;
		}
	}

	private static bool Compare( StateTestDefinition test, float value )
	{
		foreach ( var (op, limit) in test.Compare )
		{
			if ( limit is not { } bound )
				continue;
			var ok = op switch
			{
				"<" => value < bound,
				"<=" => value <= bound,
				">" => value > bound,
				">=" => value >= bound,
				"==" => MathF.Abs( value - bound ) < 0.0001f,
				">|" => MathF.Abs( value ) > bound,
				_ => true
			};
			if ( !ok )
				return false;
		}
		return true;
	}

	// True when the dotted names of `pattern` appear in order, contiguously, in the dotted path.
	private static bool Contains( string path, string pattern )
	{
		if ( string.IsNullOrEmpty( pattern ) )
			return false;
		var names = path.Split( '.' );
		var wanted = pattern.Split( '.' );
		for ( var start = 0; start + wanted.Length <= names.Length; start++ )
		{
			var match = true;
			for ( var i = 0; i < wanted.Length && match; i++ )
				match = names[ start + i ].Equals( wanted[ i ], StringComparison.OrdinalIgnoreCase );
			if ( match )
				return true;
		}
		return false;
	}

	private static Node Build( StateGraphDefinition definition, Node parent, int index, int depth )
	{
		var node = new Node { Definition = definition, Parent = parent, Index = index, Depth = depth };
		node.Children = definition.States.Select( ( child, i ) => Build( child, node, i, depth + 1 ) ).ToArray();
		return node;
	}

	private static void Resolve( Node node )
	{
		node.Transitions = node.Definition.Transitions
			.Select( ( transition, order ) => (transition, order, target: Find( node, transition.Target )) )
			.OrderByDescending( t => t.transition.Priority ).ThenBy( t => t.order )
			.Select( t => (t.transition, t.target) ).ToArray();
		foreach ( var child in node.Children )
			Resolve( child );
	}

	// A dotted path starts at a child of the owner or of its nearest ancestor that has such a child; failing
	// that, at the nearest state of that name anywhere below an ancestor.
	private static Node Find( Node owner, string path )
	{
		if ( string.IsNullOrEmpty( path ) )
			return null;
		var names = path.Split( '.' );
		for ( var scope = owner; scope is not null; scope = scope.Parent )
		{
			var found = Follow( scope.Children.Where( child => child.Definition.Name.Equals( names[ 0 ], StringComparison.OrdinalIgnoreCase ) ), names );
			if ( found is not null )
				return found;
			if ( scope.Parent is null && scope.Definition.Name.Equals( names[ 0 ], StringComparison.OrdinalIgnoreCase ) )
				return Follow( new[] { scope }, names );
		}
		for ( var scope = owner; scope is not null; scope = scope.Parent )
		{
			foreach ( var start in Descendants( scope ) )
			{
				if ( !start.Definition.Name.Equals( names[ 0 ], StringComparison.OrdinalIgnoreCase ) )
					continue;
				var node = start;
				for ( var i = 1; i < names.Length && node is not null; i++ )
					node = node.Children.FirstOrDefault( child => child.Definition.Name.Equals( names[ i ], StringComparison.OrdinalIgnoreCase ) );
				if ( node is not null )
					return node;
			}
		}
		return null;
	}

	private static Node Follow( IEnumerable<Node> starts, string[] names )
	{
		foreach ( var start in starts )
		{
			var node = start;
			for ( var i = 1; i < names.Length && node is not null; i++ )
				node = node.Children.FirstOrDefault( child => child.Definition.Name.Equals( names[ i ], StringComparison.OrdinalIgnoreCase ) );
			if ( node is not null )
				return node;
		}
		return null;
	}

	// Breadth first, so the nearest match wins.
	private static IEnumerable<Node> Descendants( Node scope )
	{
		var queue = new Queue<Node>( scope.Children );
		while ( queue.Count > 0 )
		{
			var node = queue.Dequeue();
			yield return node;
			foreach ( var child in node.Children )
				queue.Enqueue( child );
		}
	}
}
