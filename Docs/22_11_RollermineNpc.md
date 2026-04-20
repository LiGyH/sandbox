# 22.11 — NPC: Роллермайн (RollermineNpc) 🔵

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

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

	[Sync] public bool IsHunting { get; private set; }
	private TimeSince _lastBounce;
	private const float BounceCooldown = 0.25f;

	private SphereCollider _collider;
	private float _baseRadius;

	/// <summary>
	/// Called by chase/idle schedules to toggle the hunting particle children.
	/// On entering hunt, the collider grows and an upward impulse is applied so the
	/// roller "pops" into action.
	/// </summary>
	public void SetHunting( bool hunting )
	{
		if ( IsHunting == hunting ) return;
		IsHunting = hunting;

		if ( HuntingEffects.IsValid() )
			HuntingEffects.Enabled = hunting;

		if ( _collider.IsValid() )
		{
			_collider.Radius = hunting ? _baseRadius * 1.4f : _baseRadius;

			if ( hunting && Rigidbody.IsValid() )
				Rigidbody.ApplyImpulse( Vector3.Up * 50000f );
		}
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
		_collider = GetComponent<SphereCollider>();

		if ( _collider.IsValid() )
			_baseRadius = _collider.Radius;

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

> **Примечание о сетевизации.** Поле `_hunting` заменено на публичное свойство `[Sync] IsHunting`, чтобы клиенты тоже могли реагировать на состояние охоты — это нужно для нового компонента `RollermineMorphs` (см. ниже). Кроме того, `SetHunting( true )` теперь временно увеличивает радиус `SphereCollider` в 1.4 раза и применяет короткий импульс вверх, благодаря чему ролик «вскакивает» при обнаружении цели.

---

# 🔵 Морфы и свечение Rollermine (RollermineMorphs)

## Что мы делаем?
Создаём `RollermineMorphs` — компонент, который управляет морф-таргетами и пульсацией свечения материала роллермайна. Использует **ту же** меш-модель, что и морф-вариант ховербола (`Coils_Deployed`, `Pins_Deployed`), но реагирует на `IsHunting`.

## Зачем это нужно?
Визуально показывает «боевой режим»: при включении охоты у роллермайна выдвигаются катушки и шипы, материал начинает мерцать характерным бирюзовым self-illum.

## Как это работает внутри движка?
- Берёт ссылку на `RollermineNpc` и `SkinnedModelRenderer` в дочернем объекте.
- Если задан `GlowMaterial`, копирует его и подменяет `MaterialOverride`, отключив батчинг.
- В `OnUpdate()` целевые значения морфов = 1, если `IsHunting`, иначе 0.
- Переходы — через `Easing.BounceOut`, длительность `TransitionDuration = 0.3 сек`.
- Свечение работает аналогично `HoverballMorphs`: бирюзовый `IllumTint`, шумно мерцающая яркость, гасится множителем `_coils`.

## Создай файл
`Code/Npcs/Rollermine/RollermineMorphs.cs`

```csharp
using Sandbox.Utility;

namespace Sandbox.Npcs.Rollermine;

/// <summary>
/// Drives morph targets and material glow on the rollermine mesh based on hunting state.
/// Same mesh as hoverball — uses Coils_Deployed and Pins_Deployed morphs.
/// </summary>
public sealed class RollermineMorphs : Component
{
	private RollermineNpc _rollermine;
	private SkinnedModelRenderer _renderer;
	private Material _glowMaterialCopy;

	private float _coils;
	private float _pins;
	private float _brightnessTarget;
	private float _brightnessCurrent;
	private float _brightnessTimer;

	private float _coilsFrom;
	private float _coilsTo;
	private float _coilsTime;
	private float _pinsFrom;
	private float _pinsTo;
	private float _pinsTime;

	[Property] public float Speed { get; set; } = 15f;
	public float TransitionDuration => 0.3f;
	[Property] public Material GlowMaterial { get; set; }

	public Color IllumTint => Color.FromBytes( 20, 165, 200 );
	public float IllumBrightness => 8f;

	protected override void OnStart()
	{
		_rollermine = GetComponent<RollermineNpc>();
		_renderer = GetComponentInChildren<SkinnedModelRenderer>();

		if ( GlowMaterial is not null && _renderer.IsValid() )
		{
			_glowMaterialCopy = GlowMaterial.CreateCopy();
			_renderer.MaterialOverride = _glowMaterialCopy;
			_renderer.SceneModel.Batchable = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( !_rollermine.IsValid() || !_renderer.IsValid() ) return;

		var hunting = _rollermine.IsHunting;

		var targetCoils = hunting ? 1f : 0f;
		var targetPins = hunting ? 1f : 0f;

		if ( targetCoils != _coilsTo )
		{
			_coilsFrom = _coils;
			_coilsTo = targetCoils;
			_coilsTime = 0f;
		}

		if ( targetPins != _pinsTo )
		{
			_pinsFrom = _pins;
			_pinsTo = targetPins;
			_pinsTime = 0f;
		}

		_coilsTime = Math.Min( _coilsTime + Time.Delta / TransitionDuration, 1f );
		_pinsTime = Math.Min( _pinsTime + Time.Delta / TransitionDuration, 1f );

		_coils = MathX.Lerp( _coilsFrom, _coilsTo, Easing.BounceOut( _coilsTime ) );
		_pins = MathX.Lerp( _pinsFrom, _pinsTo, Easing.BounceOut( _pinsTime ) );

		_renderer.SceneModel?.Morphs.Set( "Coils_Deployed", _coils );
		_renderer.SceneModel?.Morphs.Set( "Pins_Deployed", _pins );

		UpdateGlowMaterial();
	}

	void UpdateGlowMaterial()
	{
		if ( _glowMaterialCopy is null ) return;

		var hunting = _rollermine.IsHunting;
		var brightness = hunting ? IllumBrightness : 0f;

		if ( hunting )
		{
			_brightnessTimer -= Time.Delta;
			if ( _brightnessTimer <= 0f )
			{
				_brightnessTarget = Random.Shared.Float( 6f, 8f );
				_brightnessTimer = Random.Shared.Float( 0.1f, 0.4f );
			}
			_brightnessCurrent = MathX.Approach( _brightnessCurrent, _brightnessTarget, Time.Delta * 7f );
			brightness = _brightnessCurrent;
		}

		_glowMaterialCopy.Set( "g_vSelfIllumTint", hunting ? IllumTint : Color.Black );
		_glowMaterialCopy.Set( "g_flSelfIllumBrightness", brightness * _coils );
	}
}
```

## Проверка
- При обнаружении игрока ролик «раскрывается» — морфы плавно идут к 1, материал начинает мерцать бирюзовым.
- При потере цели и возврате к Idle — морфы возвращаются к 0, свечение гаснет.
- Переходы используют `Easing.BounceOut`, поэтому появление пинов выглядит как «щелчок».

---

Следующий шаг: [15.01 — Система сохранения (SaveSystem)](23_02_SaveSystem.md)

---

<!-- seealso -->
## 🔗 См. также

- [04.01 — GameManager](04_01_GameManager.md)
- [23.02 — SaveSystem](23_02_SaveSystem.md)
- [02.04 — Damage Tags](02_04_DamageTags.md)

