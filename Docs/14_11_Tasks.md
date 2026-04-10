# 14.11 — NPC: Задачи (Tasks) ✅

## Что мы делаем?

Создаём набор конкретных задач, которые NPC выполняет в рамках расписаний.

## Задача: Wait (Ожидание)

Путь: `Code/Npcs/Tasks/Wait.cs`

```csharp
namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that waits for a specified duration
/// </summary>
public class Wait : TaskBase
{
	public float Duration { get; set; }
	private TimeUntil _endTime;

	public Wait( float duration )
	{
		Duration = duration;
	}

	protected override void OnStart()
	{
		_endTime = Duration;
	}

	protected override TaskStatus OnUpdate()
	{
		return _endTime ? TaskStatus.Success : TaskStatus.Running;
	}
}
```

## Задача: LookAt (Посмотреть на)

Путь: `Code/Npcs/Tasks/LookAt.cs`

```csharp
namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Sets a persistent look target on the AnimationLayer and waits until the NPC is facing it.
/// </summary>
public class LookAt : TaskBase
{
	public Vector3? TargetPosition { get; set; }
	public GameObject TargetObject { get; set; }

	public LookAt( Vector3 targetPosition )
	{
		TargetPosition = targetPosition;
	}

	public LookAt( GameObject gameObject )
	{
		TargetObject = gameObject;
	}

	protected override void OnStart()
	{
		if ( TargetObject.IsValid() )
			Npc.Animation.SetLookTarget( TargetObject );
		else if ( TargetPosition.HasValue )
			Npc.Animation.SetLookTarget( TargetPosition.Value );
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !TargetObject.IsValid() && !TargetPosition.HasValue )
			return TaskStatus.Failed;

		return Npc.Animation.IsFacingTarget() ? TaskStatus.Success : TaskStatus.Running;
	}
}
```

## Задача: MoveTo (Идти к)

Путь: `Code/Npcs/Tasks/MoveTo.cs`

```csharp
using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that commands the NavigationLayer to move to a target position or GameObject.
/// When tracking a GameObject, re-evaluates the path periodically.
/// </summary>
public class MoveTo : TaskBase
{
	public Vector3? TargetPosition { get; set; }
	public GameObject TargetObject { get; set; }
	public float StopDistance { get; set; } = 10f;
	public float ReevaluateInterval { get; set; } = 0.5f;
	public float LateralThreshold { get; set; } = 60f;

	private TimeSince _lastReevaluate;

	public MoveTo( Vector3 targetPosition, float stopDistance = 10f )
	{
		TargetPosition = targetPosition;
		StopDistance = stopDistance;
	}

	public MoveTo( GameObject targetObject, float stopDistance = 10f )
	{
		TargetObject = targetObject;
		StopDistance = stopDistance;
	}

	protected override void OnStart()
	{
		var pos = GetTargetPosition();
		if ( !pos.HasValue ) return;

		Npc.Navigation.MoveTo( pos.Value, StopDistance );
		_lastReevaluate = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		// Target object destroyed mid-move
		if ( TargetObject is not null && !TargetObject.IsValid() )
			return TaskStatus.Failed;

		// Re-evaluate path for moving targets
		if ( TargetObject.IsValid() && _lastReevaluate > ReevaluateInterval )
		{
			var pos = GetTargetPosition();
			if ( pos.HasValue )
				Npc.Navigation.MoveTo( pos.Value, StopDistance );
			_lastReevaluate = 0;
		}

		var agent = Npc.Navigation.Agent;
		if ( agent.IsValid() && agent.Velocity.WithZ( 0 ).Length > 1f )
		{
			var moveDir = agent.Velocity.WithZ( 0 ).Normal;
			var fwd = Npc.WorldRotation.Forward.WithZ( 0 ).Normal;
			var angle = Vector3.GetAngle( fwd, moveDir );

			if ( angle > LateralThreshold && !Npc.Animation.LookTarget.HasValue )
			{
				// No look target — face the movement direction
				var targetRot = Rotation.LookAt( moveDir, Vector3.Up );
				Npc.GameObject.WorldRotation = Rotation.Lerp(
					Npc.WorldRotation, targetRot, Npc.Animation.LookSpeed * Time.Delta );
			}
		}

		return Npc.Navigation.GetStatus();
	}

	private Vector3? GetTargetPosition()
	{
		if ( TargetObject.IsValid() )
		{
			var bounds = TargetObject.GetBounds();
			return bounds.ClosestPoint( Npc.WorldPosition );
		}

		return TargetPosition;
	}
}
```

## Задача: Say (Говорить)

Путь: `Code/Npcs/Tasks/Say.cs`

```csharp
﻿using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that plays speech via the SpeechLayer. Waits for the speech to finish before completing.
/// </summary>
public class Say : TaskBase
{
	public SoundEvent Sound { get; set; }
	public string Message { get; set; }
	public float Duration { get; set; }

	public Say( SoundEvent sound, float duration = 0f )
	{
		Sound = sound;
		Duration = duration;
	}

	public Say( string message, float duration = 3f )
	{
		Message = message;
		Duration = duration;
	}

	protected override void OnStart()
	{
		var speech = Npc.Speech;

		if ( Sound is not null )
		{
			speech.Say( Sound, Duration );
		}
		else if ( !string.IsNullOrEmpty( Message ) )
		{
			speech.Say( Message, Duration );
		}
	}

	protected override TaskStatus OnUpdate()
	{
		return Npc.Speech.IsSpeaking ? TaskStatus.Running : TaskStatus.Success;
	}
}
```

## Задача: FireWeapon (Стрелять)

Путь: `Code/Npcs/Tasks/FireWeapon.cs`

```csharp
using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Shoots a weapon at a target for a specific duration
/// </summary>
public class FireWeapon : TaskBase
{
	public BaseWeapon Weapon { get; }
	public GameObject Target { get; }
	public float BurstDuration { get; }
	public float AimTurnSpeed { get; set; } = 8f;

	private TimeUntil _burstEnd;

	public FireWeapon( BaseWeapon weapon, GameObject target, float burstDuration = 1.5f )
	{
		Weapon = weapon;
		Target = target;
		BurstDuration = burstDuration;
	}

	protected override void OnStart()
	{
		_burstEnd = BurstDuration;
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !Weapon.IsValid() ) return TaskStatus.Failed;
		if ( !Target.IsValid() ) return TaskStatus.Failed;

		RotateBodyTowardTarget();

		// Only fire once we're actually facing the target
		if ( Npc.Animation.IsFacingTarget() && Weapon.CanPrimaryAttack() )
		{
			Weapon.PrimaryAttack();
			Npc.Animation.TriggerAttack();
		}

		return _burstEnd ? TaskStatus.Success : TaskStatus.Running;
	}

	private void RotateBodyTowardTarget()
	{
		var toTarget = (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 );
		if ( toTarget.LengthSquared < 1f ) return;

		var targetRot = Rotation.LookAt( toTarget.Normal, Vector3.Up );
		Npc.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRot, AimTurnSpeed * Time.Delta );
	}
}
```

## Задачи: PickUpProp / DropProp

Путь: `Code/Npcs/Tasks/PickUpProp.cs`

```csharp
﻿namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Tells the AnimationLayer to pick up and hold a prop.
/// </summary>
public class PickUpProp : TaskBase
{
	public GameObject Target { get; set; }

	public PickUpProp( GameObject target )
	{
		Target = target;
	}

	protected override void OnStart()
	{
		Npc.Animation.SetHeldProp( Target );
	}

	protected override TaskStatus OnUpdate()
	{
		return TaskStatus.Success;
	}
}
```

Путь: `Code/Npcs/Tasks/DropProp.cs`

```csharp
﻿namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Tells the AnimationLayer to drop the held prop.
/// </summary>
public class DropProp : TaskBase
{
	public GameObject Target { get; set; }

	public DropProp( GameObject target )
	{
		Target = target;
	}

	protected override void OnStart()
	{
		Npc.Animation.ClearHeldProp();
	}

	protected override TaskStatus OnUpdate()
	{
		return TaskStatus.Success;
	}
}
```

## Сводка задач

| Задача | Описание | Завершение |
|--------|----------|-----------|
| `Wait` | Ждёт N секунд | По истечении времени |
| `LookAt` | Поворачивается к цели | Когда лицом к цели |
| `MoveTo` | Идёт к точке/объекту | Когда дошёл (NavMesh) |
| `Say` | Произносит фразу | Когда речь закончена |
| `FireWeapon` | Стреляет очередью | По истечении BurstDuration |
| `PickUpProp` | Берёт предмет | Мгновенно |
| `DropProp` | Бросает предмет | Мгновенно |

---

Следующий шаг: [14.12 — NPC: Боевой NPC (CombatNpc)](14_12_CombatNpc.md)
