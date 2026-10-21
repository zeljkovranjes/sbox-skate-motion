using System.Collections.Generic;
using System.Linq;

namespace SkateMotion.Core.Animation;

using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;
using Matrix4x4 = System.Numerics.Matrix4x4;

/// <summary>
/// Finds the humanoid bones of an arbitrary skeleton: hips, spine chain, neck, head, and per side the shoulder,
/// arm, forearm, hand, thigh, shin, foot and toe, and each hand's fingers (thumb, index, middle, ring and pinky, each
/// with an optional base bone and up to three joints). Names are matched against common rig conventions after
/// stripping prefixes and separators; side comes from the name or, failing that, from rest geometry; roles a
/// name does not reveal are recovered from the hierarchy (the limb chains between hips, chest and the ends).
/// Also measures the rig's facing so models built facing any direction retarget correctly.
/// </summary>
internal sealed class HumanoidBones
{
	/// <summary>Role names shared with clip skeletons.</summary>
	internal static readonly string[] Spine = { "spine", "spine1", "spine2", "spine3" };

	private static readonly string[] Limbs = { "shoulder", "arm", "forearm", "hand", "upleg", "leg", "foot", "toe" };

	/// <summary>Finger role names, in the order fingers lie across a hand from the thumb.</summary>
	internal static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "pinky" };

	// Words naming each finger, in the order of Fingers.
	private static readonly string[][] FingerWords =
	{
		new[] { "thumb", "pollex" }, new[] { "index", "pointer" }, new[] { "middle", "mid" }, new[] { "ring" },
		new[] { "pinky", "pinkie", "little", "small" }
	};

	// Words that mark a finger's base (metacarpal) bone, and the end markers that close a finger without a role.
	private static readonly string[] FingerBase = { "meta", "metacarpal", "carpal", "palm", "base" };
	private static readonly string[] EndMarkers = { "end", "nub", "tip" };

	// Helper words that belong to a hand's fingers rather than to something else hanging from the hand.
	private static readonly string[] FingerHelpers = { "finger", "thumb", "index", "middle", "ring", "pinky", "little", "carpal",
		"meta", "end", "nub", "tip" };

	// A first bone starting this close to the wrist (share of the finger's reach) ahead of three more is its base.
	private const float BaseReach = 0.35f;

	// Words that mark helper bones (twist, roll, IK targets, end markers, corrective, fingers) which never take a role.
	private static readonly string[] Helpers =
	{
		"twist", "roll", "ik", "target", "pole", "end", "nub", "tip", "helper", "correct", "adjust", "jiggle", "dyn", "phys",
		"finger", "thumb", "index", "middle", "ring", "pinky", "little", "carpal", "meta", "attach", "weapon", "prop", "socket",
		"eye", "jaw", "lid", "brow", "lip", "tongue", "teeth", "ear", "hair", "cloth", "skirt", "breast", "bust", "butt",
		"dummy", "null", "aux", "share", "bend", "rot", "scale", "offset", "point", "mch", "org", "wgt"
	};

	private readonly string[] _names;
	private readonly int[] _parents;
	private readonly Vector3[] _bind;
	private readonly List<int>[] _children;
	private readonly Dictionary<string, int> _roles = new( StringComparer.OrdinalIgnoreCase );

	/// <param name="names">Bone names.</param>
	/// <param name="parents">Parent indices, -1 for roots.</param>
	/// <param name="bind">Rest positions in model space.</param>
	internal HumanoidBones( string[] names, int[] parents, Vector3[] bind )
	{
		_names = names;
		_parents = parents;
		_bind = bind;
		_children = names.Select( _ => new List<int>() ).ToArray();
		for ( var bone = 0; bone < parents.Length; bone++ )
		{
			if ( parents[ bone ] >= 0 )
				_children[ parents[ bone ] ].Add( bone );
		}
		var words = names.Select( Words ).ToArray();
		var sides = words.Select( Side ).ToArray();
		var roles = words.Select( Role ).ToArray();
		Preset = ApplyPreset( roles, sides );
		FindHips( roles );
		FindLegs( roles, sides );
		FindTorso( roles, sides );
		FindFingers();
		Facing = MeasureFacing();
	}

	/// <summary>Name of the rig family whose bone names matched, or null when found by heuristics alone.</summary>
	internal string Preset { get; }

	/// <summary>Rotation from the clip frame (+X forward, +Y left, +Z up) to the rig's model space.</summary>
	internal Quaternion Facing { get; }

	/// <summary>True when hips, both legs down to the feet and a head were found.</summary>
	internal bool Complete => new[] { "hips", "head", "left_upleg", "left_leg", "left_foot", "right_upleg", "right_leg", "right_foot" }
		.All( _roles.ContainsKey );

	/// <summary>Target bone index of a role, or -1.</summary>
	internal int this[ string role ] => _roles.TryGetValue( role, out var bone ) ? bone : -1;

	/// <summary>Found roles by target bone name.</summary>
	internal Dictionary<string, string> Map()
	{
		var map = new Dictionary<string, string>( StringComparer.Ordinal );
		foreach ( var (role, bone) in _roles )
			map[ _names[ bone ] ] = role;
		return map;
	}

	// A rig family whose names cover the body decides each named bone's role and side outright.
	private string ApplyPreset( string[] roles, string[] sides )
	{
		var required = new[] { "hips", "left_upleg", "left_leg", "left_foot", "right_upleg", "right_leg", "right_foot",
			"left_arm", "left_forearm", "left_hand", "right_arm", "right_forearm", "right_hand" };
		RigPresets.Preset best = null;
		Dictionary<string, int> bestRoles = null;
		var bestScore = 0;
		foreach ( var preset in RigPresets.All )
		{
			var byName = new Dictionary<string, int>( StringComparer.Ordinal );
			for ( var bone = 0; bone < _names.Length; bone++ )
			{
				var name = _names[ bone ];
				foreach ( var pattern in preset.Namespaces )
					name = System.Text.RegularExpressions.Regex.Replace( name, pattern, "" );
				byName.TryAdd( Normalize( name ), bone );
			}
			var found = new Dictionary<string, int>();
			var used = new HashSet<int>();
			foreach ( var (role, aliases) in preset.Aliases )
			{
				foreach ( var alias in aliases )
				{
					if ( byName.TryGetValue( Normalize( alias ), out var bone ) && used.Add( bone ) )
					{
						found[ role ] = bone;
						break;
					}
				}
			}
			var score = required.Count( found.ContainsKey ) * 10 + found.Count;
			if ( score > bestScore )
			{
				best = preset;
				bestRoles = found;
				bestScore = score;
			}
		}
		if ( best is null || required.Count( bestRoles.ContainsKey ) < required.Length - 1 )
			return null;
		foreach ( var (role, bone) in bestRoles )
		{
			var cut = role.IndexOf( '_' );
			sides[ bone ] = cut < 0 ? null : role[ ..cut ];
			var name = cut < 0 ? role : role[ (cut + 1).. ];
			roles[ bone ] = name.StartsWith( "spine" ) ? "spine" : name;
		}
		return best.Name;
	}

	private static string Normalize( string name ) => new( name.Where( char.IsLetterOrDigit ).Select( char.ToLowerInvariant ).ToArray() );

	private void FindHips( string[] roles )
	{
		var hips = Array.IndexOf( roles, "hips" );
		if ( hips < 0 )
		{
			// The pelvis is where the two lowest end bones (the feet) branch from each other.
			var leaves = Enumerable.Range( 0, _names.Length ).Where( bone => _children[ bone ].Count == 0 )
				.OrderBy( bone => _bind[ bone ].Z ).ToList();
			if ( leaves.Count >= 2 )
			{
				var first = leaves[ 0 ];
				var second = leaves.Skip( 1 ).FirstOrDefault( leaf => !Path( Common( first, leaf ), first ).Skip( 1 ).Contains( leaf ), -1 );
				if ( second >= 0 )
					hips = Common( first, second );
			}
		}
		if ( hips >= 0 )
			_roles[ "hips" ] = hips;
	}

	private void FindLegs( string[] roles, string[] sides )
	{
		if ( !_roles.TryGetValue( "hips", out var hips ) )
			return;
		// The legs end in the two lowest end bones below the hips that hang from different branches.
		var leaves = Enumerable.Range( 0, _names.Length )
			.Where( bone => _children[ bone ].Count == 0 && Descends( bone, hips ) )
			.Where( bone => _bind[ bone ].Z < _bind[ hips ].Z - 0.25f * MathF.Abs( _bind[ hips ].Z ) )
			.OrderBy( bone => _bind[ bone ].Z ).ToList();
		if ( leaves.Count < 2 )
			return;
		var first = leaves[ 0 ];
		var second = leaves.Skip( 1 ).FirstOrDefault( leaf => Common( first, leaf ) is var fork && fork != first && fork != leaf
			&& (fork == hips || Descends( fork, hips )) && Branch( fork, leaf ) != Branch( fork, first ), -1 );
		if ( second < 0 )
			return;
		var split = Common( first, second );
		var legs = new[] { (Child: Branch( split, first ), End: first), (Child: Branch( split, second ), End: second) };
		var named = legs.Select( leg => ChainSide( leg.Child, leg.End, sides ) ).ToArray();
		if ( named[ 0 ] is null || named[ 1 ] is null || named[ 0 ] == named[ 1 ] )
		{
			// Unnamed sides: the leg on the left of the walking direction (toes point forward) is the left one.
			var forward = Forward( legs[ 0 ].Child, legs[ 0 ].End ) + Forward( legs[ 1 ].Child, legs[ 1 ].End );
			var left = Vector3.Cross( Vector3.UnitZ, forward );
			var firstLeft = Vector3.Dot( _bind[ legs[ 0 ].Child ] - _bind[ legs[ 1 ].Child ], left ) > 0f;
			named = new[] { firstLeft ? "left" : "right", firstLeft ? "right" : "left" };
		}
		for ( var i = 0; i < 2; i++ )
			AssignChain( Path( legs[ i ].Child, legs[ i ].End ), named[ i ], new[] { "upleg", "leg", "foot", "toe" }, roles );
	}

	// The child of ancestor on the way down to bone.
	private int Branch( int ancestor, int bone )
	{
		for ( ; bone >= 0; bone = _parents[ bone ] )
		{
			if ( _parents[ bone ] == ancestor )
				return bone;
		}
		return -1;
	}

	private void FindTorso( string[] roles, string[] sides )
	{
		if ( !_roles.TryGetValue( "hips", out var hips ) )
			return;
		var legBones = new HashSet<int>( new[] { "left_upleg", "right_upleg" }.Where( _roles.ContainsKey ).Select( role => _roles[ role ] ) );
		var head = Enumerable.Range( 0, _names.Length ).FirstOrDefault( bone => roles[ bone ] == "head" && Descends( bone, hips ), -1 );
		if ( head < 0 )
		{
			// The head is the highest bone above the hips outside the legs.
			head = Enumerable.Range( 0, _names.Length )
				.Where( bone => Descends( bone, hips ) && !legBones.Any( leg => bone == leg || Descends( bone, leg ) ) )
				.OrderByDescending( bone => _bind[ bone ].Z ).FirstOrDefault( -1 );
			// Prefer the bone where the skull starts rather than a crown end bone.
			while ( head >= 0 && _children[ head ].Count == 0 && _parents[ head ] >= 0 && _parents[ head ] != hips
				&& roles[ _parents[ head ] ] is null or "head" )
				head = _parents[ head ];
		}
		if ( head < 0 )
			return;
		_roles[ "head" ] = head;
		if ( (_bind[ head ] - _bind[ hips ]).LengthSquared() > 1e-8f )
			_up = Vector3.Normalize( _bind[ head ] - _bind[ hips ] );
		var trunk = Path( hips, head ).Skip( 1 ).ToList();
		trunk.Remove( head );
		// Arms branch off the trunk sideways; their ends are the bones farthest from the trunk.
		var arms = new List<(int Branch, int Root, int End)>();
		foreach ( var (bone, index) in trunk.Select( ( bone, index ) => (bone, index) ) )
		{
			foreach ( var child in _children[ bone ] )
			{
				if ( index + 1 < trunk.Count && child == trunk[ index + 1 ] || child == head )
					continue;
				var end = Farthest( child, bone );
				var reach = Lateral( _bind[ end ] - _bind[ bone ] );
				if ( reach > 0.2f * MathF.Max( _bind[ head ].Z - _bind[ hips ].Z, 0.0001f ) && Path( child, end ).Count >= 3 )
					arms.Add( (bone, child, end) );
			}
		}
		arms = arms.OrderByDescending( arm => Lateral( _bind[ arm.End ] - _bind[ arm.Branch ] ) ).Take( 2 ).ToList();
		var chest = arms.Count > 0 ? arms.Select( arm => trunk.IndexOf( arm.Branch ) ).Max() : trunk.Count - 1;
		var neckStart = chest + 1;
		var spine = trunk.Take( Math.Max( chest + 1, 0 ) ).ToList();
		var neck = trunk.Skip( neckStart ).ToList();
		if ( spine.Count == 0 && neck.Count > 0 )
		{
			spine.Add( neck[ 0 ] );
			neck.RemoveAt( 0 );
		}
		// Spread the rig's spine over the clip's four spine bones: the first and last always line up.
		for ( var i = 0; i < spine.Count; i++ )
		{
			var slot = spine.Count == 1 ? Spine.Length - 1 : (int)MathF.Round( i * (Spine.Length - 1f) / (spine.Count - 1f) );
			_roles.TryAdd( Spine[ slot ], spine[ i ] );
		}
		if ( neck.Count > 0 )
		{
			_roles[ "neck" ] = neck[ 0 ];
			if ( neck.Count > 1 )
				_roles[ "neck1" ] = neck[ ^1 ];
		}
		var armSides = arms.Select( arm => ChainSide( arm.Root, arm.End, sides ) ).ToArray();
		if ( arms.Count == 2 && (armSides[ 0 ] is null || armSides[ 0 ] == armSides[ 1 ]) )
		{
			var left = Vector3.Transform( Vector3.UnitY, MeasureFacing() );
			var firstLeft = Vector3.Dot( _bind[ arms[ 0 ].End ] - _bind[ arms[ 1 ].End ], left ) > 0f;
			armSides = new[] { firstLeft ? "left" : "right", firstLeft ? "right" : "left" };
		}
		for ( var i = 0; i < arms.Count; i++ )
		{
			if ( armSides[ i ] is not null )
				AssignArm( arms[ i ].Root, arms[ i ].End, armSides[ i ], roles );
		}
	}

	// An arm chain runs shoulder, arm, forearm, hand, then fingers; names decide when they can, else the
	// hand is the first bone that fans out into several children (or the last but one).
	private void AssignArm( int root, int end, string side, string[] roles )
	{
		var chain = Path( root, end );
		var hand = chain.FindIndex( bone => roles[ bone ] == "hand" );
		if ( hand < 0 )
			hand = chain.FindIndex( bone => _children[ bone ].Count > 1 && chain.IndexOf( bone ) >= 2 );
		if ( hand < 0 )
			hand = Math.Max( chain.Count - 1, 2 );
		var limb = chain.Take( hand + 1 ).Where( bone => !IsHelper( bone ) ).ToList();
		var names = limb.Count >= 4 ? new[] { "shoulder", "arm", "forearm", "hand" } : new[] { "arm", "forearm", "hand" };
		var slots = limb.Skip( Math.Max( 0, limb.Count - names.Length ) ).ToList();
		AssignChain( slots, side, names, roles );
	}

	// Fingers hang from each hand: every chain of two or more bones below it that is not a helper, and a bone that
	// fans out into several chains is a palm carrying them. A finger's name decides which it is (thumb, index,
	// middle, ring, pinky or little, or a number counting from the thumb); otherwise the fingers are ordered across
	// the hand from the thumb, the chain pointing furthest from the others. A first bone named as a metacarpal, or
	// starting near the wrist ahead of three more, is the finger's base; the next three are its joints.
	private void FindFingers()
	{
		foreach ( var side in new[] { "left", "right" } )
		{
			if ( !_roles.TryGetValue( $"{side}_hand", out var hand ) )
				continue;
			var chains = new List<List<int>>();
			CollectFingers( hand, chains );
			if ( chains.Count == 0 )
				continue;
			var named = NameFingers( chains );
			if ( named.Any( finger => finger < 0 ) || named.Distinct().Count() != named.Length )
				named = OrderFingers( hand, chains );
			for ( var i = 0; i < chains.Count; i++ )
			{
				if ( named[ i ] >= 0 )
					AssignFinger( hand, chains[ i ], side, Fingers[ named[ i ] ] );
			}
		}
	}

	private void CollectFingers( int bone, List<List<int>> chains )
	{
		foreach ( var child in _children[ bone ] )
		{
			if ( !FingerBone( child ) )
				continue;
			var chain = new List<int> { child };
			var at = child;
			while ( _children[ at ].Count( FingerBone ) == 1 )
			{
				at = _children[ at ].First( FingerBone );
				chain.Add( at );
			}
			if ( _children[ at ].Count( FingerBone ) > 1 )
				CollectFingers( at, chains );
			else if ( chain.Count( link => !EndMarker( link ) ) >= 2 )
				chains.Add( chain );
		}
	}

	// Which finger each chain's names reveal, by index into Fingers, or -1. Numbered fingers count from the thumb,
	// from zero or one as the rig numbers them.
	private int[] NameFingers( List<List<int>> chains )
	{
		var named = chains.Select( chain => chain.Select( bone => Words( _names[ bone ] ) )
			.Select( words => Array.FindIndex( FingerWords, finger => words.Any( finger.Contains ) ) )
			.FirstOrDefault( finger => finger >= 0, -1 ) ).ToArray();
		if ( named.All( finger => finger >= 0 ) )
			return named;
		var numbers = chains.Select( chain => FingerNumber( chain[ 0 ] ) ).ToArray();
		if ( numbers.Any( number => number < 0 ) )
			return named;
		var first = numbers.Min();
		return numbers.Select( number => number - first < Fingers.Length ? number - first : -1 ).ToArray();
	}

	// The number after "finger" in a bone name (its first digit, which a finger's own joints share), or -1.
	private int FingerNumber( int bone )
	{
		var words = Words( _names[ bone ] );
		var at = Array.IndexOf( words, "finger" );
		return at >= 0 && at + 1 < words.Length && char.IsDigit( words[ at + 1 ][ 0 ] ) ? words[ at + 1 ][ 0 ] - '0' : -1;
	}

	// Unnamed fingers: the thumb points furthest from the mean of all the fingers; the rest follow by how far their
	// first bone lies from the thumb's.
	private int[] OrderFingers( int hand, List<List<int>> chains )
	{
		var directions = chains.Select( chain => _bind[ chain[ ^1 ] ] - _bind[ hand ] )
			.Select( offset => offset.LengthSquared() > 1e-10f ? Vector3.Normalize( offset ) : Vector3.Zero ).ToArray();
		var mean = directions.Aggregate( Vector3.Zero, ( sum, direction ) => sum + direction );
		var thumb = Enumerable.Range( 0, chains.Count ).OrderBy( i => Vector3.Dot( directions[ i ], mean ) ).First();
		var order = Enumerable.Range( 0, chains.Count ).Where( i => i != thumb )
			.OrderBy( i => Vector3.Distance( _bind[ chains[ i ][ 0 ] ], _bind[ chains[ thumb ][ 0 ] ] ) ).ToList();
		var named = new int[ chains.Count ];
		named[ thumb ] = 0;
		for ( var i = 0; i < order.Count; i++ )
			named[ order[ i ] ] = i + 1 < Fingers.Length ? i + 1 : -1;
		return named;
	}

	private void AssignFinger( int hand, List<int> chain, string side, string finger )
	{
		var bones = chain.Where( bone => !EndMarker( bone ) ).ToList();
		var start = 0;
		var reach = Vector3.Distance( _bind[ chain[ ^1 ] ], _bind[ hand ] );
		if ( finger != "thumb" && bones.Count >= 2 && (Words( _names[ bones[ 0 ] ] ).Any( FingerBase.Contains )
			|| bones.Count >= 4 && Vector3.Distance( _bind[ bones[ 0 ] ], _bind[ hand ] ) < BaseReach * reach) )
		{
			_roles.TryAdd( $"{side}_{finger}_base", bones[ 0 ] );
			start = 1;
		}
		for ( var joint = 0; joint < 3 && start + joint < bones.Count; joint++ )
			_roles.TryAdd( $"{side}_{finger}{joint + 1}", bones[ start + joint ] );
	}

	// Bones a finger may run through: anything but helpers of other kinds (twist, IK, attachments).
	private bool FingerBone( int bone ) =>
		!Words( _names[ bone ] ).Any( word => Helpers.Contains( word ) && !FingerHelpers.Contains( word ) );

	private bool EndMarker( int bone ) => Words( _names[ bone ] ).Any( EndMarkers.Contains );

	private void AssignChain( List<int> chain, string side, string[] slots, string[] roles )
	{
		chain = chain.Where( bone => !IsHelper( bone ) ).ToList();
		var used = new HashSet<int>();
		// Named bones claim their role first, then the rest fill the remaining slots in chain order.
		foreach ( var slot in slots )
		{
			var named = chain.FirstOrDefault( bone => roles[ bone ] == slot && !used.Contains( bone ), -1 );
			if ( named < 0 )
				continue;
			_roles[ $"{side}_{slot}" ] = named;
			used.Add( named );
		}
		var free = chain.Where( bone => !used.Contains( bone ) ).ToList();
		foreach ( var slot in slots )
		{
			if ( _roles.ContainsKey( $"{side}_{slot}" ) || free.Count == 0 )
				continue;
			var previous = slots.TakeWhile( other => other != slot ).Select( other => _roles.GetValueOrDefault( $"{side}_{other}", -1 ) ).DefaultIfEmpty( -1 ).Max();
			var next = free.FirstOrDefault( bone => previous < 0 || Descends( bone, previous ), -1 );
			if ( next < 0 )
				continue;
			_roles[ $"{side}_{slot}" ] = next;
			free.Remove( next );
		}
	}

	private Quaternion MeasureFacing()
	{
		var hips = this[ "hips" ];
		var head = this[ "head" ];
		var left = this[ "left_upleg" ];
		var right = this[ "right_upleg" ];
		if ( hips < 0 || head < 0 || left < 0 || right < 0 )
			return Quaternion.Identity;
		var up = Vector3.Normalize( _bind[ head ] - _bind[ hips ] );
		var side = _bind[ left ] - _bind[ right ];
		side -= up * Vector3.Dot( side, up );
		if ( side.LengthSquared() < 1e-8f )
			return Quaternion.Identity;
		side = Vector3.Normalize( side );
		var forward = Vector3.Cross( side, up );
		var basis = new Matrix4x4(
			forward.X, forward.Y, forward.Z, 0f,
			side.X, side.Y, side.Z, 0f,
			up.X, up.Y, up.Z, 0f,
			0f, 0f, 0f, 1f );
		var facing = Quaternion.Normalize( Quaternion.CreateFromRotationMatrix( basis ) );
		// Snap near-axis facings so a rig built standing straight keeps an exact frame.
		return Snap( facing );
	}

	private static Quaternion Snap( Quaternion facing )
	{
		var forward = Vector3.Transform( Vector3.UnitX, facing );
		var up = Vector3.Transform( Vector3.UnitZ, facing );
		if ( MathF.Abs( up.Z ) < 0.99f )
			return facing;
		var yaw = MathF.Atan2( forward.Y, forward.X );
		var quarter = MathF.Round( yaw / (MathF.PI / 2f) ) * (MathF.PI / 2f);
		return MathF.Abs( yaw - quarter ) < 0.05f ? Quaternion.CreateFromAxisAngle( Vector3.UnitZ, quarter ) : facing;
	}

	private Vector3 Forward( int root, int end )
	{
		// A foot points forward from the ankle; use the last two bones of the leg chain.
		var chain = Path( root, end );
		if ( chain.Count < 2 )
			return Vector3.UnitX;
		var tip = _bind[ chain[ ^1 ] ] - _bind[ chain[ ^2 ] ];
		tip.Z = 0f;
		return tip.LengthSquared() > 1e-8f ? Vector3.Normalize( tip ) : Vector3.UnitX;
	}

	private Vector3 _up = Vector3.UnitZ;

	// Distance from the trunk's up axis.
	private float Lateral( Vector3 offset ) => (offset - _up * Vector3.Dot( offset, _up )).Length();

	private string ChainSide( int root, int end, string[] sides ) =>
		Path( root, end ).Select( bone => sides[ bone ] ).FirstOrDefault( side => side is not null );

	private bool IsHelper( int bone ) => Words( _names[ bone ] ).Any( word => Helpers.Contains( word ) );

	private int Lowest( int bone )
	{
		var best = bone;
		foreach ( var child in _children[ bone ] )
		{
			var low = Lowest( child );
			if ( _bind[ low ].Z < _bind[ best ].Z )
				best = low;
		}
		return best;
	}

	private int Farthest( int bone, int from )
	{
		var best = bone;
		foreach ( var child in _children[ bone ] )
		{
			var far = Farthest( child, from );
			if ( Lateral( _bind[ far ] - _bind[ from ] ) > Lateral( _bind[ best ] - _bind[ from ] ) )
				best = far;
		}
		return best;
	}

	private bool Descends( int bone, int ancestor )
	{
		for ( bone = _parents[ bone ]; bone >= 0; bone = _parents[ bone ] )
		{
			if ( bone == ancestor )
				return true;
		}
		return false;
	}

	private int Common( int a, int b )
	{
		var ancestors = new HashSet<int>();
		for ( var bone = a; bone >= 0; bone = _parents[ bone ] )
			ancestors.Add( bone );
		for ( var bone = b; bone >= 0; bone = _parents[ bone ] )
		{
			if ( ancestors.Contains( bone ) )
				return bone;
		}
		return -1;
	}

	// Bones from root down to end, inclusive.
	private List<int> Path( int root, int end )
	{
		var path = new List<int>();
		for ( var bone = end; bone >= 0; bone = _parents[ bone ] )
		{
			path.Add( bone );
			if ( bone == root )
				break;
		}
		path.Reverse();
		return path.Count > 0 && path[ 0 ] == root ? path : new List<int>();
	}

	/// <summary>Lower-case words of a bone name: namespaces dropped, split at separators, case changes and digits.</summary>
	internal static string[] Words( string name )
	{
		var cut = name.LastIndexOfAny( new[] { ':', '|', '/' } );
		if ( cut >= 0 )
			name = name[ (cut + 1).. ];
		var words = new List<string>();
		var current = new System.Text.StringBuilder();
		void Flush()
		{
			if ( current.Length > 0 )
				words.Add( current.ToString().ToLowerInvariant() );
			current.Clear();
		}
		for ( var i = 0; i < name.Length; i++ )
		{
			var c = name[ i ];
			if ( !char.IsLetterOrDigit( c ) )
			{
				Flush();
				continue;
			}
			if ( current.Length > 0 )
			{
				var last = current[ ^1 ];
				var upper = char.IsUpper( c ) && char.IsLower( last );
				var digit = char.IsDigit( c ) != char.IsDigit( last );
				if ( upper || digit )
					Flush();
			}
			current.Append( c );
		}
		Flush();
		// Common rig prefixes carry no meaning.
		while ( words.Count > 1 && words[ 0 ] is "bip" or "01" or "001" or "valve" or "b" or "j" or "jnt" or "bn" or "def" or "c" or "valvebiped" or "mixamorig" or "cc" or "base" or "rig" or "skel" or "sk" or "bone" or "biped" )
			words.RemoveAt( 0 );
		return words.ToArray();
	}

	private static string Side( string[] words )
	{
		foreach ( var word in words )
		{
			if ( word is "left" or "l" or "lf" or "lft" )
				return "left";
			if ( word is "right" or "r" or "rt" or "rgt" )
				return "right";
		}
		var joined = string.Concat( words );
		if ( joined.StartsWith( "left" ) )
			return "left";
		if ( joined.StartsWith( "right" ) )
			return "right";
		return null;
	}

	/// <summary>The humanoid role a bone name reveals (without side), or null.</summary>
	internal static string Role( string[] words )
	{
		if ( words.Any( word => Helpers.Contains( word ) ) )
			return null;
		var core = words.Where( word => word is not ("left" or "right" or "l" or "r" or "lf" or "rt" or "lft" or "rgt") && !word.All( char.IsDigit ) ).ToArray();
		var joined = string.Concat( core ).Replace( "left", "" ).Replace( "right", "" );
		bool Has( params string[] parts ) => parts.Any( part => joined.Contains( part ) );
		if ( joined is "pelvis" or "hips" or "hip" or "cog" or "hipsroot" )
			return "hips";
		if ( Has( "thigh", "upleg", "upperleg", "legupper", "femur" ) )
			return "upleg";
		if ( Has( "calf", "shin", "knee", "lowerleg", "leglower", "tibia" ) )
			return "leg";
		if ( Has( "toe", "ball" ) )
			return "toe";
		if ( Has( "foot", "ankle" ) )
			return "foot";
		if ( Has( "clavicle", "shoulder", "collar" ) )
			return "shoulder";
		if ( Has( "forearm", "lowerarm", "armlower", "elbow", "ulna", "radius" ) )
			return "forearm";
		if ( Has( "upperarm", "armupper", "uparm", "humerus", "bicep" ) || joined == "arm" )
			return "arm";
		if ( Has( "hand", "wrist", "palm" ) )
			return "hand";
		if ( Has( "head", "skull" ) )
			return "head";
		if ( Has( "neck" ) )
			return "neck";
		if ( Has( "spine", "chest", "torso", "abdomen", "belly", "waist", "back" ) )
			return "spine";
		if ( joined == "leg" )
			return "leg";
		return null;
	}
}
