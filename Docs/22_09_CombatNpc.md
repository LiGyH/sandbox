# 22.09 — NPC: Боевой NPC (CombatNpc) ⚔️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)

## Что мы делаем?

Создаём **CombatNpc** — боевого NPC, который патрулирует, обнаруживает игроков, преследует и стреляет.

## Расписания CombatNpc

```
GetSchedule()
    ↓
┌─ Видимый враг? → CombatEngageSchedule
├─ Последнее местоположение? → ScientistSearchSchedule
└─ Ничего → CombatPatrolSchedule
```

## Создай файл: CombatNpc

Путь: `Code/Npcs/Combat/CombatNpc.cs`

```csharp
using Sandbox.Npcs.Schedules;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// A combat NPC that searches for players, advances on them, fires in bursts, and repositions.
/// </summary>
public class CombatNpc : Npc, Component.IDamageable
{
	private static readonly string[] PainLines =
	{
		"Argh!",
		"They got me!",
		"I'm hit!",
		"Taking fire!",
		"Ugh!",
	};

	private static readonly string[] DeathLines =
	{
		"Tell them... I fought...",
		"Not like this...",
		"I can't...",
	};

	[Property, ClientEditable, Range( 1, 250 ), Sync]
	public float Health { get; set; } = 100f;

	/// <summary>
	/// The weapon this NPC uses to attack.
	/// </summary>
	[Property]
	public BaseWeapon Weapon { get; set; }

	[Property, Group( "Balance" ), Range( 512, 4096 ), Step( 1 ), ClientEditable, Sync]
	public float AttackRange { get; set; } = 1024f;

	[Property, Group( "Balance" ), Range( 90, 250f ), Step( 1 ), ClientEditable, Sync]
	public float EngageSpeed { get; set; } = 180f;

	[Property, Group( "Balance" )]
	public float SearchTimeout { get; set; } = 8f;

	[Property, Group( "Balance" )]
	public float PatrolRadius { get; set; } = 400f;

	[Property, Group( "Balance" )]
	public float BurstDuration { get; set; } = 1.5f;

	[Property, Group( "Balance" )]
	public float BurstPause { get; set; } = 0.8f;

	private Vector3? _lastKnownPosition;
	private TimeSince _timeSinceLastSeen;

	protected override void OnStart()
	{
		base.OnStart();

		if ( Weapon.IsValid() && Renderer.IsValid() )
		{
			Weapon.CreateWorldModel( Renderer );

			if ( !IsProxy )
				Animation.SetHoldType( Weapon.HoldType );
		}
	}

	public override ScheduleBase GetSchedule()
	{
		var visible = Senses.GetNearestVisible();

		if ( visible.IsValid() )
		{
			_lastKnownPosition = visible.WorldPosition;
			_timeSinceLastSeen = 0;

			var engage = GetSchedule<CombatEngageSchedule>();
			engage.Target = visible;
			engage.Weapon = Weapon;
			engage.AttackRange = AttackRange;
			engage.EngageSpeed = EngageSpeed;
			engage.BurstDuration = BurstDuration;
			engage.BurstPause = BurstPause;
			return engage;
		}

		// Player not visible — search last known position if recent enough
		if ( _lastKnownPosition.HasValue && _timeSinceLastSeen < SearchTimeout )
		{
			var search = GetSchedule<ScientistSearchSchedule>();
			search.Target = _lastKnownPosition.Value;
			return search;
		}

		// No intel — patrol
		var patrol = GetSchedule<CombatPatrolSchedule>();
		patrol.PatrolRadius = PatrolRadius;
		return patrol;
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy )
			return;

		Health -= damage.Damage;

		// If we can hear the attacker, treat their position as the last known location
		if ( damage.Attacker.IsValid() )
		{
			var dist = WorldPosition.Distance( damage.Attacker.WorldPosition );
			if ( dist <= Senses.HearingRange )
			{
				_lastKnownPosition = damage.Attacker.WorldPosition;
				_timeSinceLastSeen = 0;
			}
		}

		if ( Health < 1f )
		{
			if ( Speech.CanSpeak )
				Speech.Say( Game.Random.FromArray( DeathLines ), 2f );

			Die( damage );
			return;
		}

		if ( Speech.CanSpeak && Game.Random.Float() < 0.5f )
			Speech.Say( Game.Random.FromArray( PainLines ), 1.5f );

		// Interrupt current schedule so we react immediately
		EndCurrentSchedule();
	}
}
```

## Создай файл: CombatEngageSchedule

Путь: `Code/Npcs/Combat/CombatEngageSchedule.cs`

```csharp
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Engages a visible player: close the gap, fire a burst, pause, then reposition to a flanking point.
/// Cancels immediately if the target leaves sight.
/// </summary>
public class CombatEngageSchedule : ScheduleBase
{
	private static readonly string[] SpotLines =
	{
		"Contact!", "There!", "I see you!", "Got one!",
		"Enemy spotted!", "Don't move!", "Found you.",
	};

	private static readonly string[] TauntLines =
	{
		"You're not getting away!", "Stay down!", "Take cover!",
		"Suppressing fire!", "Keep the pressure on!", "Don't let up!",
		"That's for my squad!", "You picked the wrong fight.",
	};

	public GameObject Target { get; set; }
	public BaseWeapon Weapon { get; set; }
	public float AttackRange { get; set; } = 300f;
	public float BurstDuration { get; set; } = 1.5f;
	public float BurstPause { get; set; } = 0.8f;
	public float EngageSpeed { get; set; } = 180f;
	public float FlankRadius { get; set; } = 250f;

	protected override void OnStart()
	{
		Npc.Navigation.WishSpeed = EngageSpeed;
		Npc.Animation.SetLookTarget( Target );

		if ( Npc.Speech.CanSpeak )
			AddTask( new Say( Game.Random.FromArray( SpotLines ), 1.5f ) );

		AddTask( new LookAt( Target ) );
		AddTask( new MoveTo( Target, AttackRange ) );
		AddTask( new FireWeapon( Weapon, Target, BurstDuration ) );

		if ( Npc.Speech.CanSpeak && Game.Random.Float() < 0.4f )
			AddTask( new Say( Game.Random.FromArray( TauntLines ), BurstPause ) );
		else
			AddTask( new Wait( BurstPause ) );

		AddTask( new MoveTo( GetFlankPosition(), 20f ) );
	}

	protected override void OnEnd()
	{
		Npc.Navigation.WishSpeed = 100f;
		Npc.Animation.ClearLookTarget();
	}

	protected override bool ShouldCancel()
	{
		if ( !Target.IsValid() ) return true;
		return !Npc.Senses.VisibleTargets.Contains( Target );
	}

	private Vector3 GetFlankPosition()
	{
		Vector3 toTarget = Target.IsValid()
			? (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 ).Normal
			: Npc.WorldRotation.Forward;

		var perp = new Vector3( -toTarget.y, toTarget.x, 0 );
		var side = Game.Random.Float() > 0.5f ? 1f : -1f;
		var flankDir = (perp * side + toTarget * 0.3f).WithZ( 0 ).Normal;
		var candidate = Npc.WorldPosition + flankDir * Game.Random.Float( FlankRadius * 0.5f, FlankRadius );

		if ( Npc.Scene.NavMesh.GetClosestPoint( candidate ) is { } nav )
			return nav;

		return candidate;
	}
}
```

## Создай файл: CombatPatrolSchedule

Путь: `Code/Npcs/Combat/CombatPatrolSchedule.cs`

```csharp
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.CombatNpc;

/// <summary>
/// Wanders to random nearby points when no player is known.
/// </summary>
public class CombatPatrolSchedule : ScheduleBase
{
	private static readonly string[] PatrolLines =
	{
		"Stay sharp.", "Keep moving.", "All clear so far.",
		"Eyes open.", "Nothing yet.", "Where'd they go...",
		"Something's not right.", "I'll check over here.",
	};

	public float PatrolRadius { get; set; } = 400f;

	protected override void OnStart()
	{
		var dest = GetPatrolDestination();
		AddTask( new MoveTo( dest, 15f ) );

		if ( Npc.Speech.CanSpeak && Game.Random.Float() < 0.2f )
			AddTask( new Say( Game.Random.FromArray( PatrolLines ), 2.5f ) );
		else
			AddTask( new Wait( Game.Random.Float( 1f, 2.5f ) ) );
	}

	protected override bool ShouldCancel()
	{
		return Npc.Senses.GetNearestVisible().IsValid();
	}

	private Vector3 GetPatrolDestination()
	{
		var dir = Vector3.Random.WithZ( 0 ).Normal;
		var dist = Game.Random.Float( PatrolRadius * 0.3f, PatrolRadius );
		var candidate = Npc.WorldPosition + dir * dist;

		if ( Npc.Scene.NavMesh.GetClosestPoint( candidate ) is { } nav )
			return nav;

		return candidate;
	}
}
```

## Логика поведения

| Ситуация | Расписание | Задачи |
|----------|-----------|--------|
| Видит врага | CombatEngage | Say → LookAt → MoveTo → FireWeapon → Pause → MoveTo (фланг) |
| Потерял врага | ScientistSearch | MoveTo (последняя позиция) → LookAt → Wait |
| Никого нет | CombatPatrol | MoveTo (случайная точка) → Say/Wait |

---

Следующий шаг: [14.13 — NPC: Учёный (ScientistNpc)](22_10_ScientistNpc.md)

---

<!-- seealso -->
## 🔗 См. также

- [06.04 — BaseWeapon](06_04_BaseWeapon.md)
- [02.04 — Damage Tags](02_04_DamageTags.md)

