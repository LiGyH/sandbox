# 22.10 — NPC: Учёный (ScientistNpc) 🔬

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)

## Что мы делаем?

Создаём **ScientistNpc** — мирного NPC, который бродит по карте, осматривает предметы, подбирает маленькие объекты, а при получении урона — паникует и убегает.

## Система страха

```
Урон → _peakFear увеличивается (до 1.0)
    ↓
FearGracePeriod (3 сек) — страх на максимуме
    ↓
Затухание: -FearDecayRate (0.15) в секунду
    ↓
Страх = 0 → возврат к обычному поведению
```

## Расписания учёного

```
GetSchedule()
    ↓
┌─ Страх > 0 и есть атакующий? → ScientistFleeSchedule
├─ Случайно (35%) → ScientistWanderSchedule  
├─ Случайно (25%) и есть проп → ScientistInspectPropSchedule
└─ Иначе → ScientistIdleSchedule
```

## Создай файлы

### ScientistNpc.cs

Путь: `Code/Npcs/Scientist/ScientistNpc.cs`

```csharp
﻿using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.Scientist;

public class ScientistNpc : Npc, Component.IDamageable
{
	[Property, ClientEditable, Range( 1, 100 ), Sync]
	public float Health { get; set; } = 100f;

	public float AfraidLevel
	{
		get
		{
			if ( _peakFear <= 0f ) return 0f;
			if ( _timeSinceHurt <= FearGracePeriod ) return _peakFear;

			var decayTime = _timeSinceHurt - FearGracePeriod;
			return MathF.Max( _peakFear - decayTime * FearDecayRate, 0f );
		}
	}

	[Property, Group( "Balance" )]
	private float FearGracePeriod { get; set; } = 3f;

	[Property, Group( "Balance" )]
	private float FearDecayRate { get; set; } = 0.15f;

	private float _peakFear;
	private GameObject _attacker;
	private TimeSince _timeSinceHurt;
	private bool _isFleeing;

	public override ScheduleBase GetSchedule()
	{
		var fear = AfraidLevel;

		if ( fear <= 0f && _peakFear > 0f )
		{
			_peakFear = 0f;
			_attacker = null;
		}

		if ( fear > 0f && _attacker.IsValid() )
		{
			if ( !_isFleeing )
			{
				_isFleeing = true;
				_attacker.GetComponent<Player>()?.PlayerData?.AddStat( "npc.scientist.scare" );
			}

			var flee = GetSchedule<ScientistFleeSchedule>();
			flee.Source = _attacker;
			flee.PanicLevel = fear;
			return flee;
		}

		_isFleeing = false;
		return GetIdleSchedule();
	}

	private ScheduleBase GetIdleSchedule()
	{
		var roll = Game.Random.Float();

		if ( roll < 0.35f )
			return GetSchedule<ScientistWanderSchedule>();

		if ( roll < 0.60f )
		{
			var prop = FindNearbyProp();
			if ( prop.IsValid() )
			{
				var inspect = GetSchedule<ScientistInspectPropSchedule>();
				inspect.PropTarget = prop;
				return inspect;
			}
		}

		return GetSchedule<ScientistIdleSchedule>();
	}

	private GameObject FindNearbyProp()
	{
		var nearby = Scene.FindInPhysics( new Sphere( WorldPosition, 2048 ) );

		GameObject best = null;
		float bestDist = float.MaxValue;

		foreach ( var obj in nearby )
		{
			if ( obj == GameObject ) continue;
			if ( obj.GetComponent<Prop>() is null ) continue;

			var dist = WorldPosition.Distance( obj.WorldPosition );
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = obj;
			}
		}

		return best;
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;

		Health -= damage.Damage;

		_peakFear = MathF.Min( _peakFear + damage.Damage / 50f, 1f );
		_attacker = damage.Attacker;
		_timeSinceHurt = 0;

		EndCurrentSchedule();

		if ( Health < 1 )
		{
			_attacker?.GetComponent<Player>()?.PlayerData?.AddStat( "npc.scientist.kill" );
			Die( damage );
		}
	}
}
```

### ScientistIdleSchedule.cs

Путь: `Code/Npcs/Scientist/ScientistIdleSchedule.cs`

```csharp
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistIdleSchedule : ScheduleBase
{
	private static readonly string[] IdleLines =
	[
		"...", "Hmm.", "*yawn*", "What a day.", "Need more coffee.",
	];

	protected override void OnStart()
	{
		var forward = GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;
		var yawOffset = Game.Random.Float( -90f, 90f );
		var lookDir = Rotation.FromAxis( Vector3.Up, yawOffset ) * forward;
		var lookTarget = GameObject.WorldPosition + lookDir * 100f;
		AddTask( new LookAt( lookTarget ) );

		var speech = Npc.Speech;
		if ( speech is not null && speech.CanSpeak && Game.Random.Float() < 0.15f )
		{
			var line = IdleLines[Game.Random.Int( 0, IdleLines.Length - 1 )];
			AddTask( new Say( line, 2f ) );
		}

		AddTask( new Wait( Game.Random.Float( 1f, 3f ) ) );
	}
}
```

### ScientistWanderSchedule.cs

Путь: `Code/Npcs/Scientist/ScientistWanderSchedule.cs`

```csharp
using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistWanderSchedule : ScheduleBase
{
	private static readonly string[] WanderLines =
	[
		"Let me check over here...", "Hmm, what's over there?",
		"I should stretch my legs.", "*whistles*",
	];

	protected override void OnStart()
	{
		var randomDir = Vector3.Random.WithZ( 0 ).Normal;

		var nearest = Npc.Senses.Nearest;
		if ( nearest.IsValid() )
		{
			var toPlayer = (nearest.WorldPosition - GameObject.WorldPosition).WithZ( 0 ).Normal;
			if ( randomDir.Dot( toPlayer ) > 0.3f )
			{
				randomDir = (-toPlayer + Vector3.Random.WithZ( 0 ).Normal * 0.5f).WithZ( 0 ).Normal;
			}
		}

		var wanderTarget = GameObject.WorldPosition + randomDir * Game.Random.Float( 150f, 350f );

		AddTask( new MoveTo( wanderTarget, 15f ) );

		var speech = Npc.Speech;
		if ( speech is not null && speech.CanSpeak && Game.Random.Float() < 0.2f )
		{
			var line = WanderLines[Game.Random.Int( 0, WanderLines.Length - 1 )];
			AddTask( new Say( line, 2.5f ) );
		}

		AddTask( new Wait( Game.Random.Float( 1f, 2f ) ) );
	}
}
```

### ScientistFleeSchedule.cs

Путь: `Code/Npcs/Scientist/ScientistFleeSchedule.cs`

```csharp
using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistFleeSchedule : ScheduleBase
{
	private static readonly string[] PanicLines =
	[
		"AHHH!", "Don't hurt me!", "Help! HELP!",
		"Stay away from me!", "I'm just a scientist!",
		"Please, no!", "Somebody help!",
		"Oh god oh god oh god!", "What did I do?!", "Leave me alone!",
	];

	public GameObject Source { get; set; }
	public float PanicLevel { get; set; } = 0.5f;

	protected override void OnStart()
	{
		if ( !Source.IsValid() ) return;

		Npc.Navigation.WishSpeed = 200f + 150f * PanicLevel;
		Npc.Animation.ClearLookTarget();

		if ( Npc.Speech.CanSpeak )
		{
			var line = PanicLines[Game.Random.Int( 0, PanicLines.Length - 1 )];
			Npc.Speech.Say( line, 2f );
		}

		var awayDir = (GameObject.WorldPosition - Source.WorldPosition).WithZ( 0 ).Normal;
		var randomAngle = Game.Random.Float( -40f, 40f );
		awayDir = Rotation.FromAxis( Vector3.Up, randomAngle ) * awayDir;

		var fleeDist = 512f + 1024f * PanicLevel;
		var fleeTarget = GameObject.WorldPosition + awayDir * fleeDist;

		if ( Npc.Scene.NavMesh.GetClosestPoint( fleeTarget ) is { } navPoint )
			AddTask( new MoveTo( navPoint, 15f ) );
		else
			AddTask( new MoveTo( fleeTarget, 15f ) );
	}

	protected override void OnEnd()
	{
		Npc.Navigation.WishSpeed = 100f;
	}

	protected override bool ShouldCancel()
	{
		return !Source.IsValid();
	}
}
```

### ScientistInspectPropSchedule.cs

Путь: `Code/Npcs/Scientist/ScientistInspectPropSchedule.cs`

```csharp
using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Schedules;

public class ScientistInspectPropSchedule : ScheduleBase
{
	private static readonly string[] InspectLines =
	[
		"Hmm, interesting...", "What's this?", "Fascinating.",
		"I should take notes.", "Now how did this get here?", "Curious...",
	];

	private static readonly string[] PickUpLines =
	[
		"I'll take this.", "Ooh, let me grab this.",
		"This is coming with me.", "Mine now.",
	];

	private static readonly string[] DropLines =
	[
		"There we go.", "That goes there.", "Alright, done with that.",
	];

	public GameObject PropTarget { get; set; }

	protected override void OnStart()
	{
		if ( !PropTarget.IsValid() ) return;

		Npc.Animation.SetLookTarget( PropTarget );
		AddTask( new MoveTo( PropTarget, 40f ) );
		AddTask( new Wait( 1 ) );

		var bounds = PropTarget.GetBounds();
		var isSmall = bounds.Size.Length < 256;
		var speech = Npc.Speech;

		if ( isSmall )
		{
			if ( speech is not null && speech.CanSpeak )
			{
				var line = PickUpLines[Game.Random.Int( 0, PickUpLines.Length - 1 )];
				AddTask( new Say( line, 2f ) );
			}

			AddTask( new PickUpProp( PropTarget ) );

			var randomDir = Vector3.Random.WithZ( 0 ).Normal;
			var wanderTarget = PropTarget.WorldPosition + randomDir * Game.Random.Float( 150f, 300f );

			if ( Npc.Scene.NavMesh.GetClosestPoint( wanderTarget ) is Vector3 navPoint )
				AddTask( new MoveTo( navPoint, 15f ) );

			AddTask( new Wait( Game.Random.Float( 3f, 6f ) ) );
			AddTask( new DropProp( PropTarget ) );

			if ( speech is not null )
			{
				var line = DropLines[Game.Random.Int( 0, DropLines.Length - 1 )];
				AddTask( new Say( line, 2f ) );
			}
		}
		else
		{
			if ( speech is not null && speech.CanSpeak && Game.Random.Float() < 0.5f )
			{
				var line = InspectLines[Game.Random.Int( 0, InspectLines.Length - 1 )];
				AddTask( new Say( line, 2.5f ) );
			}

			AddTask( new Wait( Game.Random.Float( 2f, 4f ) ) );
		}
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
		Npc.Animation.ClearHeldProp();
	}
}
```

## Результат

Учёный:
- Бродит, осматривает предметы, подбирает маленькие
- При ударе — паникует и убегает, скорость зависит от уровня страха
- Страх затухает со временем → возвращается к обычному поведению

---

Следующий шаг: [14.14 — NPC: Роллермайн (RollermineNpc)](14_14_RollermineNpc.md)
