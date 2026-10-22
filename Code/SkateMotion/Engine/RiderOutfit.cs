using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace SkateMotion.Engine;

internal static class RiderOutfit
{
	/// <summary>
	/// Dresses <paramref name="body"/> as <paramref name="own"/> is dressed now: the body's look, every piece of clothing
	/// merged onto its bones with its own, and the shape a dresser gives a citizen's body. The pieces put on by an earlier
	/// call, listed in <paramref name="worn"/>, are taken off first, and the new ones listed. The body keeps its model and
	/// its height.
	/// </summary>
	internal static void CopyFrom( SkinnedModelRenderer own, SkinnedModelRenderer body, List<GameObject> worn )
	{
		foreach ( var piece in worn )
		{
			if ( piece.IsValid() )
				piece.Destroy();
		}
		worn.Clear();
		// Dressed by a dresser from an avatar, the body is dressed from that avatar the same way: its renderers carry the
		// skin and eyes as materials overriding those tagged for them, which no copy of a renderer can read (the bare legs,
		// a tagged skin, showed the model's own darker skin while the untagged head matched).
		if ( OutfitOf( own ) is { } outfit )
		{
			var before = body.GameObject.Children.ToHashSet();
			var dresser = Dresser.GetOrCreate( body );
			dresser.ApplyHeightScale = false;
			dresser.UpdateAppearance( outfit );
			dresser.Apply( outfit );
			var made = body.GameObject.Children.Where( child => !before.Contains( child ) ).ToList();
			foreach ( var piece in made )
			{
				piece.NetworkMode = NetworkMode.Never;
				piece.Flags |= GameObjectFlags.NotSaved;
				worn.Add( piece );
			}
			// What the body wears is then what the player's body wears now, piece for piece (a game may have changed it
			// since its dresser dressed it from the avatar): pieces it does not wear come off, those it wears on top are
			// copied, and its body groups (the parts its clothes hide) follow. Only the pieces just made are matched (any
			// from before are on their way out), and their renderers are found whether on or not: the rider's model is
			// hidden until it is first posed, and an enabled-only search found none, so every piece was copied as well,
			// each copy carrying the skin the player's piece had then over the dresser's own (the legs drawn brown).
			var missing = Clothing( own ).ToList();
			foreach ( var piece in made )
			{
				var renderer = piece.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf );
				if ( renderer is null || renderer.BoneMergeTarget != body )
					continue;
				var match = missing.FirstOrDefault( clothing => clothing.Model == renderer.Model );
				if ( match is null )
					piece.Destroy();
				else
					missing.Remove( match );
			}
			foreach ( var clothing in missing )
				worn.Add( Wear( clothing, body ) );
			body.BodyGroups = own.BodyGroups;
			return;
		}
		CopyLook( own, body );
		foreach ( var clothing in Clothing( own ) )
			worn.Add( Wear( clothing, body ) );
		foreach ( var shape in Shapes( own ) )
		{
			var copy = shape.Clone( new CloneConfig( body.GameObject.WorldTransform.ToWorld( shape.LocalTransform ),
				body.GameObject, false ) );
			copy.NetworkMode = NetworkMode.Never;
			copy.Flags |= GameObjectFlags.NotSaved;
			copy.Enabled = true;
			worn.Add( copy );
		}
	}

	/// <summary>A number that changes whenever anything <see cref="CopyFrom"/> copies from <paramref name="own"/> does.</summary>
	internal static int Signature( SkinnedModelRenderer own )
	{
		var hash = new HashCode();
		AddLook( ref hash, own );
		foreach ( var clothing in Clothing( own ) )
			AddLook( ref hash, clothing );
		foreach ( var deformer in Shapes( own ).SelectMany( shape => shape.Components.GetAll<ModelDeformer>( FindMode.EverythingInSelfAndDescendants ) ) )
			hash.Add( deformer.Weight );
		return hash.ToHashCode();
	}

	// A copy of a piece of clothing, merged onto the body's bones.
	private static GameObject Wear( SkinnedModelRenderer clothing, SkinnedModelRenderer body )
	{
		var piece = new GameObject( body.GameObject, true, clothing.GameObject.Name ) { NetworkMode = NetworkMode.Never };
		var renderer = piece.Components.Create<SkinnedModelRenderer>();
		CopyLook( clothing, renderer );
		renderer.DeformationMode = clothing.DeformationMode;
		renderer.BoneMergeTarget = body;
		return piece;
	}

	// The outfit the body's dresser dresses it in, as the dresser has it: from an avatar (the owner's or the local user's),
	// or its own list and appearance; null with no dresser.
	private static ClothingContainer OutfitOf( SkinnedModelRenderer own )
	{
		var dresser = own.GameObject.Root.Components.GetAll<Dresser>( FindMode.EverythingInSelfAndDescendants )
			.FirstOrDefault( found => found.BodyTarget == own );
		if ( dresser is null )
			return null;
		return dresser.Source switch
		{
			Dresser.ClothingSource.OwnerConnection => dresser.Network.Owner is { } owner
				? ClothingContainer.CreateFromConnection( owner, dresser.RemoveUnownedItems )
				: new ClothingContainer(),
			Dresser.ClothingSource.LocalUser => ClothingContainer.CreateFromLocalUser(),
			_ => ManualOutfit( dresser )
		};
	}

	private static ClothingContainer ManualOutfit( Dresser dresser )
	{
		var outfit = new ClothingContainer();
		outfit.AddRange( dresser.Clothing );
		outfit.Height = dresser.ManualHeight;
		outfit.Age = dresser.ManualAge;
		outfit.Tint = dresser.ManualTint;
		outfit.NeckSize = dresser.NeckSize;
		outfit.WaistSize = dresser.WaistSize;
		outfit.ChestSize = dresser.ChestSize;
		outfit.HeadShape = dresser.HeadShape;
		outfit.NoseSize = dresser.NoseSize;
		outfit.ChinSize = dresser.ChinSize;
		outfit.EyeColor = dresser.EyeColor;
		outfit.EyeAlign = dresser.EyeAlign;
		outfit.Normalize();
		return outfit;
	}

	// What the body wears: the renderers merged onto its bones (a held tool is not).
	private static IEnumerable<SkinnedModelRenderer> Clothing( SkinnedModelRenderer own ) =>
		own.GameObject.Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInDescendants )
			.Where( clothing => clothing != own && clothing.Model is not null && clothing.BoneMergeTarget == own );

	// The shape a dresser gives a citizen's body (neck, waist, chest, head, nose and chin): deformers kept on an object of
	// their own under the body.
	private static IEnumerable<GameObject> Shapes( SkinnedModelRenderer own ) => own.GameObject.Children
		.Where( child => child.Components.Get<ModelDeformer>( FindMode.EverythingInSelfAndDescendants ) is not null
			&& child.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) is null );

	// A renderer's look onto another: CopyFrom brings the model, body groups, material group, tint and the overrides of
	// tagged materials (a human skin); another model already on the target is kept (the rider's body has its own). Then
	// the values a dresser sets on a body and on all it wears: the skin's tone and age and the eyes' colour and
	// alignment (only those set: a body dressed without them shows the shader's defaults, as the target then does).
	private static void CopyLook( SkinnedModelRenderer from, SkinnedModelRenderer to )
	{
		if ( to.Model is null || to.Model == from.Model )
		{
			to.CopyFrom( from );
		}
		else
		{
			to.BodyGroups = from.BodyGroups;
			to.MaterialGroup = from.MaterialGroup;
			to.Tint = from.Tint;
		}
		to.MaterialOverride = from.MaterialOverride;
		var source = from.Attributes;
		var target = to.Attributes;
		if ( Set( source.GetFloat( "skin_tint", float.NaN ) ) is { } tint )
			target.Set( "skin_tint", tint );
		if ( Set( source.GetFloat( "skin_age", float.NaN ) ) is { } age )
			target.Set( "skin_age", age );
		if ( Set( source.GetVector4( "eye_color", new Vector4( float.NaN ) ) ) is { } eyes )
			target.Set( "eye_color", eyes );
		if ( Set( source.GetVector4( "eye_align", new Vector4( float.NaN ) ) ) is { } align )
			target.Set( "eye_align", new Vector2( align.x, align.y ) );
	}

	private static void AddLook( ref HashCode hash, SkinnedModelRenderer renderer )
	{
		hash.Add( renderer.Model );
		hash.Add( renderer.BodyGroups );
		hash.Add( renderer.MaterialGroup );
		hash.Add( renderer.MaterialOverride );
		hash.Add( renderer.Tint );
		hash.Add( renderer.DeformationMode );
		hash.Add( renderer.Attributes.GetFloat( "skin_tint", float.NaN ) );
		hash.Add( renderer.Attributes.GetFloat( "skin_age", float.NaN ) );
		hash.Add( renderer.Attributes.GetVector4( "eye_color", new Vector4( float.NaN ) ) );
		hash.Add( renderer.Attributes.GetVector4( "eye_align", new Vector4( float.NaN ) ) );
	}

	// A value read back with a NaN default: null when it was never set.
	private static float? Set( float value ) => float.IsNaN( value ) ? null : value;

	private static Vector4? Set( Vector4 value ) => float.IsNaN( value.x ) ? null : value;

	internal static void ApplyDefault( SkinnedModelRenderer renderer )
	{
		var clothing = new ClothingContainer();
		Add( clothing, "models/citizen_clothes/shirt/tshirt/tshirt.clothing" );
		Add( clothing, "models/citizen_clothes/trousers/jeans/jeans.clothing" );
		Add( clothing, "models/citizen_clothes/shoes/sneakers/sneakers.clothing" );
		var dresser = renderer.Components.Create<Dresser>();
		dresser.BodyTarget = renderer;
		dresser.ApplyHeightScale = false;
		dresser.Apply( clothing );
	}

	private static void Add( ClothingContainer outfit, string path )
	{
		var item = ResourceLibrary.Get<Clothing>( path );
		if ( item is null )
		{
			Log.Warning( $"Skate Motion: default rider clothing is unavailable: {path}" );
			return;
		}
		outfit.Add( item );
	}
}
