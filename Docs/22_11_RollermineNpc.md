# 14.14 — NPC: Роллермайн (RollermineNpc) 🔵

## Что мы делаем?

Создаём **RollermineNpc** — физического NPC-мину в форме шара, который катится к игрокам, прыгает на них и наносит урон при контакте. Не использует NavMesh — всё на физических силах.

## Расписания Rollermine

```
GetSchedule()
    ↓
┌─ Видит цель? → RollermineChaseSchedule (Roll → Leap → повтор)
└─ Не видит → RollermineIdleSchedule (Wait)
```

## Создай файл: RollermineNpc

Путь: `Code/Npcs/Rollermine/RollermineNpc.cs`

```csharp
using Sandbox.Npcs.Layers;
using Sandbox.Npcs.Rollermine.Schedules;

namespace Sandbox.Npcs.Rollermine;

/// <summary>
/// A physics-driven NPC that chases players, leaps at them, and bounces off dealing damage on contact.
/// </summary>
public class RollermineNpc : Npc, Component.IDamageable, Component.ICollisionListener
{
	[Property, ClientEditable, Range( 1f, 500f ), Sync]
	public float Health { get; set; } = 35f;

	[Property, Group( "Balance" )]
	public float RollForce { get; set; } = 80000f;

	[Property, Group( "Balance" )]
	public float RollTorque { get; set; } = 40000f;

	[Property, Group( "Balance" )]
	public float StuckJumpForce { get; set; } = 500f;

	[Property, Group( "Balance" )]
	public float LeapForce { get; set; } = 60000f;

	[Property, Group( "Balance" )]
	public float LeapUpwardBias { get; set; } = 0.2f;

	[Property, Group( "Balance" )]
	public float BounceForce { get; set; } = 450f;

	[Property, Group( "Balance" )]
	public float ContactDamage { get; set; } = 20f;

	[Property, Group( "Balance" )]
	public float LeapRange { get; set; } = 160f;

	[Property]
	public GameObject Eye { get; set; }

	[Property, Group( "Effects" )]
	public GameObject HuntingEffects { get; set; }

	[Property, Group( "Effects" )]
	public GameObject LeapEffect { get; set; }

	[Property, Group( "Effects" )]
	public GameObject ContactEffect { get; set; }

	[Property, Group( "Effects" )]
	public SoundEvent RollSound { get; set; }

	[Property, Group( "Effects" )]
	public float PitchSpeedMax { get; set; } = 600f;

	[Property, Group( "Effects" )]
	public float PitchMin { get; set; } = 0.6f;

	[Property, Group( "Effects" )]
	public float PitchMax { get; set; } = 1.6f;

	private SoundHandle _rollSound;

	public Rigidbody Rigidbody { get; private set; }

	private bool _hunting;
	private TimeSince _lastBounce;
	private const float BounceCooldown = 0.25f;

	public void SetHunting( bool hunting )
	{
		if ( _hunting == hunting ) return;
		_hunting = hunting;

		if ( HuntingEffects.IsValid() )
			HuntingEffects.Enabled = hunting;
	}

	[Rpc.Broadcast]
	public void BroadcastLeapEffect()
	{
		if ( LeapEffect is null ) return;
		LeapEffect.Clone( WorldPosition );
	}

	[Rpc.Broadcast]
	public void BroadcastContactEffect( Vector3 position )
	{
		if ( ContactEffect is null ) return;
		ContactEffect.Clone( position );
	}

	protected override void OnStart()
	{
		base.OnStart();
		Rigidbody = GetComponent<Rigidbody>();

		if ( Rigidbody.IsValid() )
			Rigidbody.MotionEnabled = true;

		if ( HuntingEffects.IsValid() )
			HuntingEffects.Enabled = false;

		StartRollSound();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		StopRollSound();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		TrackEye();
		UpdateRollSound();
	}

	public override ScheduleBase GetSchedule()
	{
		var target = Senses.GetNearestVisible();
		if ( target.IsValid() )
			return GetSchedule<RollermineChaseSchedule>();

		return GetSchedule<RollermineIdleSchedule>();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;

		Health -= damage.Damage;

		if ( Health <= 0f )
			Die( damage );
	}

	protected override void Die( in DamageInfo damage )
	{
		GameManager.Current?.OnNpcDeath( DisplayName, damage );
		GameObject.Destroy();
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy ) return;
		if ( !Rigidbody.IsValid() ) return;
		if ( _lastBounce < BounceCooldown ) return;

		var root = collision.Other.GameObject?.Root;
		if ( !root.IsValid() ) return;

		if ( !root.Components.TryGet( out IDamageable damageable ) )
			return;

		_lastBounce = 0f;

		damageable.OnDamage( new DamageInfo
		{
			Damage = ContactDamage,
			Attacker = GameObject,
			Position = collision.Contact.Point,
		} );

		BroadcastContactEffect( collision.Contact.Point );

		var away = (WorldPosition - root.WorldPosition).WithZ( 0 );
		if ( away.LengthSquared < 0.01f )
			away = WorldRotation.Backward.WithZ( 0 );

		var bounceDir = (away.Normal + Vector3.Up * 2f).Normal;
		Rigidbody.Velocity = Vector3.Zero;
		Rigidbody.ApplyImpulse( bounceDir * BounceForce );
	}

	// ... Sound methods omitted for brevity, see source ...
}
```

## Создай файлы расписаний и задач

### RollermineChaseSchedule.cs

Путь: `Code/Npcs/Rollermine/RollermineChaseSchedule.cs`

```csharp
using Sandbox.Npcs.Rollermine.Tasks;

namespace Sandbox.Npcs.Rollermine.Schedules;

public class RollermineChaseSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		(Npc as RollermineNpc)?.SetHunting( true );
		AddTask( new RollermineRollTask() );
		AddTask( new RollermineLeapTask() );
	}

	protected override void OnEnd()
	{
		(Npc as RollermineNpc)?.SetHunting( false );
	}

	protected override void OnCancelled()
	{
		(Npc as RollermineNpc)?.SetHunting( false );
	}

	protected override bool ShouldCancel()
	{
		return !Npc.Senses.GetNearestVisible().IsValid();
	}
}
```

### RollermineIdleSchedule.cs

Путь: `Code/Npcs/Rollermine/RollermineIdleSchedule.cs`

```csharp
using Sandbox.Npcs.Tasks;

namespace Sandbox.Npcs.Rollermine.Schedules;

public class RollermineIdleSchedule : ScheduleBase
{
	protected override void OnStart()
	{
		AddTask( new Wait( Game.Random.Float( 1f, 2.5f ) ) );
	}

	protected override bool ShouldCancel()
	{
		return Npc.Senses.GetNearestVisible().IsValid();
	}
}
```

### RollermineRollTask.cs

Путь: `Code/Npcs/Rollermine/Tasks/RollermineRollTask.cs`

```csharp
namespace Sandbox.Npcs.Rollermine.Tasks;

/// <summary>
/// Rolls toward target using force and torque. Succeeds when within LeapRange.
/// </summary>
public class RollermineRollTask : TaskBase
{
	private const float StuckSpeedThreshold = 40f;
	private const float StuckTime = 1.2f;

	private TimeSince _stuckTimer;
	private bool _stuckTimerRunning;

	protected override TaskStatus OnUpdate()
	{
		var rollermine = Npc as RollermineNpc;
		if ( rollermine is null ) return TaskStatus.Failed;

		var rb = rollermine.Rigidbody;
		if ( !rb.IsValid() ) return TaskStatus.Failed;

		var target = Npc.Senses.GetNearestVisible();
		if ( !target.IsValid() ) return TaskStatus.Failed;

		var toTarget = target.WorldPosition - Npc.WorldPosition;
		var flatToTarget = toTarget.WithZ( 0 );
		var dist = flatToTarget.Length;

		if ( dist <= rollermine.LeapRange )
			return TaskStatus.Success;

		var dir = flatToTarget.Normal;

		rb.ApplyForce( dir * rollermine.RollForce );

		var torqueAxis = new Vector3( -dir.y, dir.x, 0f );
		rb.ApplyTorque( torqueAxis * rollermine.RollTorque );

		// Stuck detection
		var lateralSpeed = rb.Velocity.WithZ( 0 ).Length;
		if ( lateralSpeed < StuckSpeedThreshold )
		{
			if ( !_stuckTimerRunning )
			{
				_stuckTimer = 0f;
				_stuckTimerRunning = true;
			}
			else if ( _stuckTimer > StuckTime )
			{
				rb.ApplyImpulse( Vector3.Up * rollermine.StuckJumpForce );
				_stuckTimerRunning = false;
			}
		}
		else
		{
			_stuckTimerRunning = false;
		}

		return TaskStatus.Running;
	}

	protected override void Reset()
	{
		_stuckTimerRunning = false;
	}
}
```

### RollermineLeapTask.cs

Путь: `Code/Npcs/Rollermine/Tasks/RollermineLeapTask.cs`

```csharp
namespace Sandbox.Npcs.Rollermine.Tasks;

/// <summary>
/// Leaps at the current target with a clean impulse, then waits for cooldown.
/// </summary>
public class RollermineLeapTask : TaskBase
{
	private const float LeapCooldown = 1.2f;
	private TimeUntil _cooldown;

	protected override void OnStart()
	{
		var rollermine = Npc as RollermineNpc;
		if ( rollermine is null ) return;

		var rb = rollermine.Rigidbody;
		if ( !rb.IsValid() ) return;

		var target = Npc.Senses.GetNearestVisible();
		if ( !target.IsValid() ) return;

		var dir = (target.WorldPosition - Npc.WorldPosition).Normal;
		var leapDir = (dir + Vector3.Up * rollermine.LeapUpwardBias).Normal;

		rb.Velocity = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;

		rb.ApplyImpulse( leapDir * rollermine.LeapForce );

		rollermine.BroadcastLeapEffect();

		_cooldown = LeapCooldown;
	}

	protected override TaskStatus OnUpdate()
	{
		return _cooldown ? TaskStatus.Success : TaskStatus.Running;
	}
}
```

## Сводка поведения Rollermine

| Фаза | Задача | Описание |
|------|--------|----------|
| Idle | Wait | Стоит на месте 1-2.5 сек, сканирует |
| Chase: Roll | RollermineRollTask | Катится к цели, применяя силу и вращение |
| Chase: Leap | RollermineLeapTask | Прыгает на цель импульсом |
| Contact | OnCollisionStart | Наносит урон и отскакивает |

---

Следующий шаг: [15.01 — Система сохранения (SaveSystem)](15_01_SaveSystem.md)
