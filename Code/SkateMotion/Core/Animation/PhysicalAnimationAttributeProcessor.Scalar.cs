namespace SkateMotion.Core.Animation;

using Vector4 = System.Numerics.Vector4;

internal static partial class PhysicalAnimationAttributeProcessor
{
	private static void ApplyScalar( string name, PhysicalAnimationAttribute attribute, PhysicalAnimationAttributeState state )
	{
		switch ( name )
		{
			case "SLIDEBOARD":
				state.GrindName = attribute.Name;
				break;
			case "SLIDENOSE":
				state.GrindName = attribute.Name;
				break;
			case "SLIDETAIL":
				state.GrindName = attribute.Name;
				break;
			case "GRIND5050":
				state.GrindName = attribute.Name;
				break;
			case "GRIND5_O":
				state.GrindName = attribute.Name;
				break;
			case "GRINDNOSE":
				state.GrindName = attribute.Name;
				break;
			case "GRABWORLD":
				state.ActionFlags |= 0x400000UL;
				break;
			case "BOARDADJUSTLEFT":
				state.BoardAdjustment = 1;
				break;
			case "BOARDADJUSTRIGHT":
				state.BoardAdjustment = 2;
				break;
			case "BOARDADJUSTUP":
				state.BoardAdjustment = 3;
				break;
			case "BOARDADJUSTDOWN":
				state.BoardAdjustment = 4;
				break;
			case "GRINDFACINGFORWARDS":
				state.ControlFlags |= 0x80000000u;
				break;
			case "GRINDFACINGBACKWARDS":
				state.ControlFlags &= 0x7fffffffu;
				break;
			case "NOINPUT":
				state.ControlFlags |= 0x40000000u;
				state.ActionFlags |= 0x80000000000000UL;
				break;
			case "EXITGRIND":
				state.ActionFlags |= 0x20000000UL;
				break;
			case "NEWAUTOPUMP":
				state.ControlFlags |= 0x20000000u;
				break;
			case "ENTERINGCOFFIN":
				state.ActionFlags |= 0x240000000UL;
				break;
			case "INCOFFIN":
				state.ActionFlags |= 0x140000000UL;
				break;
			case "LEAVINGCOFFIN":
				state.ActionFlags |= 0xc0000000UL;
				break;
			case "PLAYINGLANDING":
				state.ActionFlags |= 0x10000000UL;
				break;
			case "BALANCE":
				state.Balance = -attribute.Value;
				break;
			case "SPIN":
				state.Spin = -attribute.Value;
				break;
			case "KICKTURN":
				state.ControlFlags |= 0x10000000u;
				break;
			case "ANTICIPATING":
				state.ControlFlags |= 0x8000000u;
				break;
			case "BODYSPIN":
				state.BodySpin = -attribute.Value;
				break;
			case "MANUALBRAKE":
				state.MotionFlags |= 0x20000000UL;
				break;
			case "BRAKE":
				state.Brake = attribute.Value;
				break;
			case "TURN":
				state.Turn = -attribute.Value;
				break;
			case "ISSTUMBLING":
				state.RigFlags |= 0x800UL;
				break;
			case "ISWIPEOUTPUSHOFFIMPULSE":
				state.RigFlags |= 0x200UL;
				break;
			case "ISWIPEOUTPUSHOFF":
				state.RigFlags |= 0x400UL;
				break;
			case "ISMUTERFOOT":
				state.RigFlags |= 0x100UL;
				break;
			case "ISMUTELFOOT":
				state.RigFlags |= 0x80UL;
				break;
			case "ISFORCEDOWNRFOOT":
				state.RigFlags |= 0x10UL;
				break;
			case "ISFORCEDOWNLFOOT":
				state.RigFlags |= 0x8UL;
				break;
			case "TURNSCALE":
				state.TurnScale = attribute.Value;
				break;
			case "MAGSCALE":
				state.MagnitudeScale = attribute.Value;
				break;
			case "ANIMENDCOMX":
				state.AnimationEndCenter = new Vector4(
					attribute.Value, state.AnimationEndCenter.Y, state.AnimationEndCenter.Z, state.AnimationEndCenter.W );
				break;
			case "ANIMENDCOMY":
				state.AnimationEndCenter = new Vector4(
					state.AnimationEndCenter.X, attribute.Value, state.AnimationEndCenter.Z, state.AnimationEndCenter.W );
				break;
			case "ANIMENDCOMZ":
				state.AnimationEndCenter = new Vector4(
					state.AnimationEndCenter.X, state.AnimationEndCenter.Y, attribute.Value, state.AnimationEndCenter.W );
				break;
			case "ANIMTRANSX":
				state.AnimationTranslation = new Vector4(
					attribute.Value, state.AnimationTranslation.Y, state.AnimationTranslation.Z, state.AnimationTranslation.W );
				break;
			case "ANIMTRANSY":
				state.AnimationTranslation = new Vector4(
					state.AnimationTranslation.X, attribute.Value, state.AnimationTranslation.Z, state.AnimationTranslation.W );
				break;
			case "ANIMTRANSZ":
				state.AnimationTranslation = new Vector4(
					state.AnimationTranslation.X, state.AnimationTranslation.Y, attribute.Value, state.AnimationTranslation.W );
				break;
			case "ANIMTIME":
				state.AnimationTime = attribute.Value;
				break;
			case "ANIMPHYSBLENDSEC":
				state.PhysicsBlendSeconds = attribute.Value;
				break;
			case "ISANIMINTERPTOEDGE":
				state.TimingFlags |= 0x1000000000000000UL;
				break;
			case "CADENCEENDPERCENT":
				state.CadenceEndPercent = attribute.Value;
				break;
			case "OB_TRAJ":
				state.ActionFlags |= 0x8000UL;
				break;
			case "OB_AIR":
				state.ActionFlags |= 0x80UL;
				break;
			case "CARVING":
				state.MotionFlags |= 0x80000000UL;
				break;
			case "RAWTURN":
				state.RawTurn = -attribute.Value;
				break;
			case "HARDTURN":
				state.HardTurn = -attribute.Value;
				break;
			case "SLIDE":
				state.Slide = -attribute.Value;
				break;
			case "FRONTFLIP":
				state.MotionFlags |= 0x80UL;
				break;
			case "BACKFLIP":
				state.MotionFlags |= 0x40UL;
				break;
			case "GRABBING":
				state.MotionFlags |= 0x20UL;
				break;
			case "WANTSLEFTAIRGRAB":
				state.MotionFlags |= 0x10UL;
				break;
			case "WANTSRIGHTAIRGRAB":
				state.MotionFlags |= 0x10UL;
				break;
			case "FLIPTRICK":
				state.ActionFlags |= 0x40000000000000UL;
				break;
			case "WIPEOUTRECOVER":
				state.ActionFlags |= 0x10000000000000UL;
				break;
			case "CONTROLLEDWIPEOUT":
				state.ActionFlags |= 0x8000000000000UL;
				break;
			case "WIPEOUT":
				state.MotionFlags |= 0x40000UL;
				break;
			case "DANGERZONE":
				state.ActionFlags |= 0x800000000000UL;
				break;
			case "LATETRICK":
				state.ActionFlags |= 0x2000000000UL;
				break;
			case "NODANGERZONE":
				state.ActionFlags |= 0x1000000000UL;
				break;
			case "CHRISTAIR":
				state.ActionFlags |= 0x4000000000UL;
				break;
			case "PHYSGRINDTRANSLATION":
				state.GrindTranslation = attribute.Value;
				break;
			case "PHYSGRINDSTABILITYNUDGE":
				state.GrindStabilityNudge = attribute.Value;
				break;
			case "PHYSGRINDUPDOWN":
				state.GrindUpDown = attribute.Value;
				break;
			case "PHYSGRINDGRABMINHEIGHT":
				state.GrindGrabMinimumHeight = attribute.Value;
				break;
			case "PHYSBODYSPIN":
				state.PhysicalBodySpin = attribute.Value;
				break;
			case "WORLD_GRAB_Y":
				state.WorldGrabY = attribute.Value;
				break;
			case "WORLD_GRAB_Z":
				state.WorldGrabZ = attribute.Value;
				break;
			case "PUSHCONTACT":
				state.TimingFlags |= 0x4000000000000000UL;
				break;
			case "NEWHANDPLANT":
				state.RigFlags |= 0x2000000000000000UL;
				break;
			case "ANIMCOMMITTEDHANDPLANT":
				state.RigFlags |= 0x1000000000000000UL;
				break;
			case "DISABLEFEETTARGETIK":
				state.RigFlags |= 0x800000000000000UL;
				break;
			case "OB_JUMP":
				state.ActionFlags |= 0x80000UL;
				break;
			case "OB_STANDINGJUMP":
				state.ActionFlags |= 0x40000UL;
				break;
			case "OB_SPRINT":
				state.RigFlags |= 0x40000UL;
				break;
			case "OB_TURN":
				state.OffboardTurn = -attribute.Value;
				break;
			case "OB_MAG":
				state.OffboardMagnitude = attribute.Value;
				break;
			case "OB_BIPEDWORLDX":
				state.BipedWorldX = attribute.Value;
				break;
			case "OB_BIPEDWORLDZ":
				state.BipedWorldZ = attribute.Value;
				break;
			case "OB_LOOKATX":
				state.LookX = attribute.Value;
				break;
			case "OB_LOOKATY":
				state.LookY = attribute.Value;
				break;
			case "OB_OBJECTMVZ":
				state.ObjectMoveZ = attribute.Value;
				break;
			case "OB_OBJECTMVX":
				state.ObjectMoveX = attribute.Value;
				break;
			case "OB_OBJECTMVROT":
				state.ObjectMoveRotation = attribute.Value;
				break;
			case "PLAYERCONTROLLEDPUMP":
				state.ActionFlags |= 0x2UL;
				break;
			case "ONEFOOTAIR":
				state.RigFlags |= 0x400000000000000UL;
				break;
			case "WIPEOUTCONTROLX":
				state.WipeoutControlX = attribute.Value;
				break;
			case "WIPEOUTCONTROLY":
				state.WipeoutControlY = attribute.Value;
				break;
			case "OB_MOUNTING":
				state.RigFlags |= 0x8000000000000UL;
				break;
			case "OB_UNMOUNTING":
				state.RigFlags |= 0x4000000000000UL;
				break;
			case "OB_DISMOUNT":
				state.RigFlags |= 0x2000000000000UL;
				break;
			case "OB_HOLDBOARD":
				state.ActionFlags |= 0x4000UL;
				break;
			case "OB_RELEASEBOARD":
				state.ActionFlags = (state.ActionFlags & ~0x3000UL) | 0x2000UL;
				break;
			case "OB_THROWBOARD":
				state.ActionFlags |= 0x3000UL;
				break;
			case "OB_RETRIEVEBOARD":
				state.ActionFlags |= 0x800UL;
				break;
			case "OB_RETRIEVINGBOARD":
				state.ActionFlags |= 0x400UL;
				break;
			case "OB_DROPPINGBOARD":
				state.ActionFlags |= 0x200UL;
				break;
			case "BIPEDBOARDONGROUND":
				state.RigFlags |= 0x2UL;
				break;
			case "ISGROUNDMOUNT":
				state.RigFlags |= 0x1UL;
				break;
			case "FOOTJUMP":
				state.RigFlags |= 0x400000000000UL;
				break;
			case "ANIMSKATEBOARD":
				state.RigFlags |= 0x800000000000UL;
				break;
			case "HIPPYJUMPING":
				state.RigFlags |= 0x200000000000UL;
				break;
			case "PUSHTRICKING":
				state.RigFlags |= 0x100000000000UL;
				break;
			case "KICKOUTDISMOUNT":
				state.RigFlags |= 0x20000000000UL;
				break;
			case "RUNOUTDISMOUNT":
				state.RigFlags |= 0x10000000000UL;
				break;
			case "FIXLEFTFOOT":
				state.RigFlags |= 0x8000000000UL;
				break;
			case "FIXRIGHTFOOT":
				state.RigFlags |= 0x4000000000UL;
				break;
			case "DOLANDONBOARD":
				state.RigFlags |= 0x2000000000UL;
				break;
			case "TRANSITIONINGONOFFBOARD":
				state.RigFlags |= 0x800000000UL;
				break;
			case "JUMPABOUTTOTAKEOFF":
				state.RigFlags |= 0x80000000UL;
				break;
			case "JUMPINPROGRESS":
				state.RigFlags |= 0x40000000UL;
				break;
			case "OB_JUMPX":
				state.OffboardJumpX = -attribute.Value;
				break;
			case "OB_JUMPZ":
				state.OffboardJumpZ = attribute.Value;
				break;
			case "SKATEROFFBOARD":
				state.RigFlags |= 0x10000000UL;
				break;
			case "ISDARK":
				state.RigFlags |= 0x400000UL;
				break;
			case "GRINDTRICK":
				state.RigFlags |= 0x200000UL;
				break;
			case "SHOVING":
				state.RigFlags |= 0x100000UL;
				break;
			case "SHOVEDIRECTION":
				state.RigFlags |= 0x80000UL;
				break;
			case "BIPEDSTARTANGLE":
				state.RigFlags |= 0x8000UL;
				state.BipedStartAngle = attribute.Value;
				break;
			case "BIPEDSPINANGLE":
				state.BipedSpinAngle = attribute.Value;
				break;
			case "BIPEDANIMTIME":
				state.BipedAnimationTime = attribute.Value;
				break;
			case "INMANUALZONE":
				state.RigFlags |= 0x4000UL;
				break;
			case "OB_TOGGLE":
				state.RigFlags |= 0x2000UL;
				break;
			case "TRICKFROMMANUAL":
				state.RigFlags |= 0x1000UL;
				break;
			case "WIPEOUTGESTURESTICKX":
				state.WipeoutGestureX = attribute.Value;
				break;
			case "WIPEOUTGESTURESTICKY":
				state.WipeoutGestureY = attribute.Value;
				break;
			case "STRONGWIPEOUTARMS":
				state.RigFlags |= 0x40UL;
				break;
			case "STRONGWIPEOUTLEGS":
				state.RigFlags |= 0x20UL;
				break;
			case "UNDERFLIPBOARDCONTACT":
				state.ControlFlags |= 0x4000000u;
				break;
			case "AUDIBLEFOOTSTEPSTRENGTH":
				state.FootstepStrength = attribute.Value;
				break;
			case "ISTAKEDOWNBYBOARD":
				state.ControlFlags |= 0x2000000u;
				break;
			case "DROPINLOCK":
				state.ControlFlags |= 0x1000000u;
				break;
			case "DROPINRELEASE":
				state.ControlFlags |= 0x800000u;
				break;
			case "OB_DROPIN":
				state.ControlFlags |= 0x400000u;
				state.TimingFlags |= 0x2000000000000000UL;
				break;
			case "BODYADJUSTZ":
				state.BodyAdjustZ = attribute.Value;
				break;
			case "BODYADJUSTX":
				state.BodyAdjustX = attribute.Value;
				break;
			case "ONEFOOTINTENT":
				state.TimingFlags |= 0x400000000000000UL;
				break;
			case "PUSHSPEED":
				state.TimingFlags |= 0x100000000000000UL;
				break;
			case "FOOTPLANTING":
				state.RigFlags |= 0x80000000000000UL;
				break;
		}
	}
}
