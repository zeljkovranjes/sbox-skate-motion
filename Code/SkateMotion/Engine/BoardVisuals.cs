using System.Collections.Generic;
using System.Linq;
using Sandbox;
using SkateMotion.Core;
using SkateMotion.Core.Constraints;

namespace SkateMotion.Engine;

internal sealed class BoardVisuals
{
	private readonly GameObject _root;
	private readonly GameObject[] _wheels = new GameObject[ 4 ];
	private readonly GameObject[] _trucks = new GameObject[ 2 ];
	private readonly BoardRig _rig;
	private readonly GameObject _board;
	private readonly GameObject _deck;
	private Transform _display;
	private float _displayWeight;
	private readonly Transform[] _wheelRest = new Transform[ 4 ];
	private readonly Transform[] _truckRest = new Transform[ 2 ];
	private bool _restCaptured;
	private readonly bool _generatedDeck;

	internal BoardVisuals( GameObject board, SkateSettings settings, float radius, BoardRig rig, bool generate )
	{
		_rig = rig;
		_board = board;
		_root = new GameObject( board, true, "Generated Skateboard" );
		if ( rig.DeckModel is not null || generate )
			_deck = AddModel( _root, "Deck", rig.DeckModel ?? BoardGeometry.Deck() );
		_generatedDeck = rig.DeckModel is null;
		var wheelModel = rig.WheelModel ?? (generate ? BoardGeometry.Wheel( radius ) : null);
		for ( var i = 0; i < 4; i++ )
			_wheels[ i ] = rig.HasCustomWheels ? rig.Wheels[ i ]
				: wheelModel is not null ? AddModel( _root, $"Wheel {i + 1}", wheelModel ) : null;
		if ( generate && !rig.HasCustomWheels )
		{
			// The generated deck's underside sits 7 mm below its centre, which rides at RideHeight.
			var truckModel = BoardGeometry.Truck( settings.Track, settings.RideHeight - 0.007f - radius );
			for ( var i = 0; i < 2; i++ )
				_trucks[ i ] = AddModel( _root, i == 0 ? "Front Truck" : "Rear Truck", truckModel );
		}
	}

	/// <summary>
	/// Shows the whole board (deck, trucks, wheels) at <paramref name="pose"/> instead of where physics holds it,
	/// blended by <paramref name="weight"/> (0 shows the physical board). Visual only.
	/// </summary>
	internal void SetDisplay( Transform pose, float weight )
	{
		// A pose that is not yet a real one (an animation's first frames) is not shown.
		var rotation = pose.Rotation;
		var length = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
		if ( !float.IsFinite( pose.Position.x + pose.Position.y + pose.Position.z ) || !float.IsFinite( length ) || length < 0.25f )
		{
			_displayWeight = 0f;
			return;
		}
		_display = pose;
		_displayWeight = Math.Clamp( weight, 0f, 1f );
	}

	/// <summary>The deck model's box in the deck's own frame (board units), or null without a deck model.</summary>
	internal BBox? DeckBounds
	{
		get
		{
			var model = _deck.IsValid() ? _deck.Components.Get<ModelRenderer>()?.Model : null;
			if ( model is null )
				return null;
			var bounds = model.Bounds;
			var local = _deck.LocalTransform;
			return BBox.FromPoints( bounds.Corners.Select( corner => local.PointToWorld( corner ) ) );
		}
	}

	/// <summary>
	/// The whole board (deck, trucks, wheels) in the deck's own frame, or null without a deck model. Trucks and wheels
	/// are counted once their rest place on the deck is known.
	/// </summary>
	internal BBox? BoardBounds
	{
		get
		{
			if ( DeckBounds is not { } bounds )
				return null;
			if ( !_restCaptured )
				return bounds;
			foreach ( var (part, rest) in _wheels.Zip( _wheelRest ).Concat( _trucks.Zip( _truckRest ) ) )
			{
				var model = part.IsValid() ? part.Components.Get<ModelRenderer>()?.Model : null;
				if ( model is null )
					continue;
				foreach ( var corner in model.Bounds.Corners )
					bounds = bounds.AddPoint( rest.PointToWorld( corner ) );
			}
			return bounds;
		}
	}

	/// <summary>
	/// Boxes of the trucks and wheels in the deck's own frame at their rest places, empty until those are known.
	/// </summary>
	internal IReadOnlyList<BBox> PartBounds
	{
		get
		{
			if ( !_restCaptured )
				return [];
			if ( _partBounds is not null )
				return _partBounds;
			var boxes = new List<BBox>();
			foreach ( var (part, rest) in _wheels.Zip( _wheelRest ).Concat( _trucks.Zip( _truckRest ) ) )
			{
				var model = part.IsValid() ? part.Components.Get<ModelRenderer>()?.Model : null;
				if ( model is not null )
					boxes.Add( BBox.FromPoints( model.Bounds.Corners.Select( corner => rest.PointToWorld( corner ) ) ) );
			}
			return _partBounds = boxes;
		}
	}

	private List<BBox> _partBounds;

	/// <summary>
	/// Top and underside of the deck model at a point of the deck's own frame (board units, X along the deck),
	/// found by tracing the model's triangles; null where the point is off the deck or there is no deck model.
	/// </summary>
	internal (float Top, float Bottom)? DeckSurface( float x, float y )
	{
		var model = _deck.IsValid() ? _deck.Components.Get<ModelRenderer>()?.Model : null;
		if ( model is null )
			return null;
		var local = _deck.LocalTransform;
		var inModel = local.PointToLocal( new Vector3( x, y, 0f ) );
		if ( _generatedDeck )
		{
			if ( BoardGeometry.DeckSurface( inModel.x / SkateUnits.InchesPerMetre, inModel.y / SkateUnits.InchesPerMetre ) is not { } shape )
				return null;
			return (local.PointToWorld( inModel.WithZ( shape.Top * SkateUnits.InchesPerMetre ) ).z,
				local.PointToWorld( inModel.WithZ( shape.Bottom * SkateUnits.InchesPerMetre ) ).z);
		}
		var reach = model.Bounds.Size.Length + 1f;
		var down = model.Trace.Ray( inModel.WithZ( model.Bounds.Maxs.z + 1f ), inModel.WithZ( model.Bounds.Mins.z - reach ) ).Run();
		var up = model.Trace.Ray( inModel.WithZ( model.Bounds.Mins.z - 1f ), inModel.WithZ( model.Bounds.Maxs.z + reach ) ).Run();
		if ( !down.Hit || !up.Hit )
			return null;
		return (local.PointToWorld( down.HitPosition ).z, local.PointToWorld( up.HitPosition ).z);
	}

	/// <summary>Where the deck is shown this frame.</summary>
	internal Transform DisplayedDeck => Blend( _shown );

	// The physical deck as shown this frame (smoothed between physics steps).
	private Transform _shown;

	internal void Update( ReadOnlySpan<DynamicBody> bodies ) => Update( bodies, true, _board.WorldTransform );

	/// <param name="bodies">Physical bodies: deck, trucks, wheels.</param>
	/// <param name="simulated">False while the board is held or carried: its bodies are not moving with it, so
	/// trucks and wheels stay on the deck at their rest offsets.</param>
	/// <param name="shown">The physical deck as shown this frame (smoothed between physics steps).</param>
	internal void Update( ReadOnlySpan<DynamicBody> bodies, bool simulated, Transform shown )
	{
		var deck = _board.WorldTransform;
		_shown = shown;
		var wheels = new Transform[ 4 ];
		var trucks = new Transform[ 2 ];
		for ( var i = 0; i < 4; i++ )
			wheels[ i ] = new Transform( SkateUnits.Inches( bodies[ i + 3 ].Position ),
				SkateUnits.ToEngine( bodies[ i + 3 ].Rotation ) * _rig.WheelRotations[ i ], _wheels[ i ].IsValid() ? _wheels[ i ].WorldScale : Vector3.One );
		for ( var i = 0; i < 2; i++ )
			trucks[ i ] = new Transform( SkateUnits.Inches( bodies[ i + 1 ].Position ),
				SkateUnits.ToEngine( bodies[ i + 1 ].Rotation ), _trucks[ i ].IsValid() ? _trucks[ i ].WorldScale : Vector3.One );
		// The first physical pose (the board as built, at rest) is where trucks and wheels sit on the deck.
		if ( !_restCaptured )
		{
			for ( var i = 0; i < 4; i++ )
				_wheelRest[ i ] = deck.ToLocal( wheels[ i ] );
			for ( var i = 0; i < 2; i++ )
				_truckRest[ i ] = deck.ToLocal( trucks[ i ] );
			_restCaptured = true;
		}
		_root.WorldTransform = Blend( shown );
		for ( var i = 0; i < 4; i++ )
		{
			if ( _wheels[ i ].IsValid() )
				_wheels[ i ].WorldTransform = simulated ? Part( wheels[ i ], _wheelRest[ i ] ) : Blend( shown ).ToWorld( _wheelRest[ i ] );
		}
		for ( var i = 0; i < 2; i++ )
		{
			if ( _trucks[ i ].IsValid() )
				_trucks[ i ].WorldTransform = simulated ? Part( trucks[ i ], _truckRest[ i ] ) : Blend( shown ).ToWorld( _truckRest[ i ] );
		}
	}

	// A truck or wheel shown on the shown deck: where physics holds it relative to the deck (steering, spin), but
	// never further from its rest place than the joints should allow, so springy joints never show the parts
	// separating at take-off or landing. While the display pose is shown it settles fully onto its rest place.
	private Transform Part( Transform physical, Transform rest )
	{
		var local = _board.WorldTransform.ToLocal( physical );
		var offset = local.Position - rest.Position;
		if ( offset.Length > PartSlack )
			local = local.WithPosition( rest.Position + offset.Normal * PartSlack );
		if ( _displayWeight > 0f )
			local = new Transform( Vector3.Lerp( local.Position, rest.Position, _displayWeight ),
				Rotation.Slerp( local.Rotation, rest.Rotation, _displayWeight ), local.Scale );
		return Blend( _shown ).ToWorld( local );
	}

	// Inches a truck or wheel may sit away from its rest place on the deck.
	private const float PartSlack = 0.15f;

	// A part's physical pose moved with the deck from its physical pose to the display pose.
	private Transform Blend( Transform physical )
	{
		if ( _displayWeight <= 0f )
			return physical;
		// A pose relative to the shown deck moved with it onto the display pose.
		var local = _shown.ToLocal( physical );
		var shown = _display.ToWorld( local );
		return new Transform( Vector3.Lerp( physical.Position, shown.Position, _displayWeight ),
			Rotation.Slerp( physical.Rotation, shown.Rotation, _displayWeight ), physical.Scale );
	}

	internal void Destroy()
	{
		if ( _root.IsValid() )
			_root.Destroy();
	}

	private static GameObject AddModel( GameObject parent, string name, Model model )
	{
		var child = new GameObject( parent, true, name );
		child.Components.Create<ModelRenderer>().Model = model;
		return child;
	}
}
