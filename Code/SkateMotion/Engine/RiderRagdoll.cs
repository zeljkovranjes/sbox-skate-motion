using Sandbox;

namespace SkateMotion.Engine;

internal sealed class RiderRagdoll
{
	private readonly SkinnedModelRenderer _renderer;
	private readonly ModelPhysics _physics;
	private float _elapsed;

	internal GameObject Object => _renderer.GameObject;
	private readonly string _pelvis;

	internal Vector3 Position => _renderer.TryGetBoneTransform( _pelvis, out var pelvis )
		? pelvis.Position : Object.WorldPosition;

	internal bool CanRecover
	{
		get
		{
			if ( _elapsed < 1f ) return false;
			foreach ( var part in _physics.Bodies )
				if ( part.Component.IsValid() && part.Component.Velocity.Length > 80f ) return false;
			return true;
		}
	}

	internal RiderRagdoll( SkinnedModelRenderer renderer, Vector3 velocity, Vector3 angularVelocity,
		Vector3 origin, float mass, string pelvis = "pelvis", GameObject board = null )
	{
		_renderer = renderer;
		_pelvis = pelvis;
		var animatedFrame = renderer.SceneModel.Transform;
		var releaseFrame = renderer.WorldTransform;
		renderer.GameObject.SetParent( renderer.Scene, true );
		renderer.GameObject.Tags.Add( SkateTags.Rider );
		_physics = renderer.Components.Create<ModelPhysics>( false );
		_physics.Renderer = renderer;
		_physics.Model = renderer.Model;
		_physics.CopyBonesFrom( renderer, true );
		_physics.Enabled = true;
		// Limp joints, as a body that has let go is: the model's ragdoll joints come with their motors holding each joint's
		// turning speed at nothing (at up to 24,000 at a knee, 62,000 at a hip), which held every joint to the pose it was
		// made in, so the body went down still crouched as it rode and lay on its back with its knees up in the air. Free of
		// them, each joint keeps the model's own friction and the legs fall flat.
		foreach ( var joint in _physics.Joints )
		{
			if ( joint.Component is BallJoint ball )
				ball.Motor = BallJoint.MotorMode.Disabled;
			else if ( joint.Component is HingeJoint hinge )
				hinge.Motor = HingeJoint.MotorMode.Disabled;
		}
		var scale = _physics.Mass > 0f ? mass / _physics.Mass : 1f;
		var centre = Vector3.Zero;
		var parts = 0;
		foreach ( var part in _physics.Bodies )
		{
			var body = part.Component;
			if ( !body.IsValid() ) continue;
			// Animation may still contain last frame's world pose when a fixed-step collision releases us.
			body.WorldTransform = releaseFrame.ToWorld( animatedFrame.ToLocal( body.WorldTransform ) );
			body.MassOverride = body.Mass * scale;
			// The engine's own gravity, as every other body in the world falls by. Pulled down by a force of ours instead,
			// with the engine's gravity off, a part that slowed for a moment (a knee at the top of its swing) fell asleep
			// where it was, as nothing the engine knew of pulled on it: the body lay on its back with its legs bent up in
			// the air.
			body.Gravity = true;
			body.EnhancedCcd = true;
			centre += body.WorldPosition;
			parts++;
		}
		// A pose shown from a frame before the board was placed somewhere else would start the body far from where the
		// board let it go (inside whatever is there); the whole pose moves to the release point instead.
		var shift = Vector3.Zero;
		if ( parts > 0 && Finite( origin ) && (centre / parts - origin).Length > StalePose )
			shift = origin + Vector3.Up * BodyAboveDeck - centre / parts;
		if ( !Finite( velocity ) ) velocity = Vector3.Zero;
		if ( !Finite( angularVelocity ) ) angularVelocity = Vector3.Zero;
		// Only the board's turn about the vertical is the rider's: a board flipping or tumbling as the rider comes off it
		// spins about its own length, and given that spin about the deck the whole body cartwheeled.
		angularVelocity = Vector3.Up * Vector3.Dot( angularVelocity, Vector3.Up );
		foreach ( var part in _physics.Bodies )
		{
			var body = part.Component;
			if ( !body.IsValid() ) continue;
			body.WorldPosition += shift;
			// Initial motion is handed off once; subsequent motion is native joint/collision dynamics.
			body.Velocity = velocity + Vector3.Cross( angularVelocity, body.WorldPosition - origin );
			body.AngularVelocity = angularVelocity;
		}
		LiftOutOfWorld( renderer, board );
	}

	// The bones of the parts are placed where the parts are, at once: clearing the clips' places cleared the places the
	// physics gave them as it took over, and until its next update the body showed in its bind pose around its root part.
	internal void ShowParts()
	{
		var scene = _renderer.SceneModel;
		if ( scene is null )
			return;
		var model = _renderer.WorldTransform;
		foreach ( var part in _physics.Bodies )
		{
			if ( part.Component.IsValid() )
				scene.SetBoneOverride( part.Bone, model.ToLocal( part.Component.WorldTransform ) );
		}
	}

	// A pose released partly inside the world (a rider upside down under the board) would start its parts under
	// the ground and fall forever; the whole body moves up by the deepest part's depth instead, keeping its pose.
	private void LiftOutOfWorld( SkinnedModelRenderer renderer, GameObject board )
	{
		var lift = 0f;
		foreach ( var part in _physics.Bodies )
		{
			var body = part.Component;
			if ( !body.IsValid() ) continue;
			var position = body.WorldPosition;
			var trace = renderer.Scene.Trace.Ray( position + Vector3.Up * LiftSearch, position )
				.IgnoreGameObjectHierarchy( renderer.GameObject );
			if ( board.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( board );
			var hit = trace.Run();
			if ( hit.Hit && !hit.StartedSolid && hit.Normal.z > 0.5f )
				lift = MathF.Max( lift, hit.HitPosition.z - position.z + LiftClearance );
		}
		if ( lift <= 0f )
			return;
		foreach ( var part in _physics.Bodies )
		{
			if ( part.Component.IsValid() )
				part.Component.WorldPosition += Vector3.Up * lift;
		}
	}

	// Farthest (inches) the posed body's centre may be from the release point before the pose counts as stale, and how
	// high above the deck (inches) a moved pose's centre is put.
	private const float StalePose = 60f;
	private const float BodyAboveDeck = 30f;

	private static bool Finite( Vector3 value ) => float.IsFinite( value.x ) && float.IsFinite( value.y ) && float.IsFinite( value.z );

	// How far above each part (inches) the ground is looked for, and the clearance left above it.
	private const float LiftSearch = 48f;
	private const float LiftClearance = 2f;

	/// <summary>Seconds since the bail.</summary>
	internal float Elapsed => _elapsed;

	/// <summary>Seconds the body has lain still (every part slower than <see cref="SettleSpeed"/>).</summary>
	internal float Settled { get; private set; }

	// Inches per second under which a body part counts as still.
	private const float SettleSpeed = 0.5f * 39.37f;

	internal void Step( float delta )
	{
		_elapsed += delta;
		var still = true;
		foreach ( var part in _physics.Bodies )
		{
			if ( part.Component.IsValid() && part.Component.Velocity.Length > SettleSpeed )
				still = false;
		}
		Settled = still ? Settled + delta : 0f;
	}

	internal void Destroy()
	{
		if ( _physics.IsValid() ) _physics.Destroy();
	}
}
