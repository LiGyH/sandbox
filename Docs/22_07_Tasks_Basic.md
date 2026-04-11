# 22_07 — Базовые задачи NPC (Wait, LookAt, MoveTo)

## Что мы делаем?

Создаём три фундаментальные задачи (tasks) для системы поведения NPC:

- **Wait** — NPC стоит на месте заданное количество секунд.
- **LookAt** — NPC поворачивается и смотрит на точку или объект.
- **MoveTo** — NPC идёт к цели, используя навигационную сетку (NavMesh).

Эти задачи — кирпичики, из которых строятся более сложные сценарии поведения.

## Как это работает внутри движка

Все задачи наследуют от `TaskBase` — абстрактного базового класса. Жизненный цикл задачи:

1. `OnStart()` — вызывается один раз при запуске задачи.
2. `OnUpdate()` — вызывается каждый кадр. Возвращает `TaskStatus`:
   - `Running` — задача ещё выполняется.
   - `Success` — задача успешно завершена.
   - `Failed` — задача провалилась.

Внутри задач доступен объект `Npc` — ссылка на NPC-контроллер, через который можно обращаться к слоям:
- `Npc.Animation` — слой анимации (управление взглядом, поворотом тела).
- `Npc.Navigation` — слой навигации (перемещение по NavMesh).

## Путь к файлу

```
Code/Npcs/Tasks/Wait.cs
Code/Npcs/Tasks/LookAt.cs
Code/Npcs/Tasks/MoveTo.cs
```

## Полный код

### `Wait.cs`

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

### `LookAt.cs`

```csharp
namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Sets a persistent look target on the AnimationLayer and waits until the NPC is facing it.
/// The look target persists after this task completes — call <see cref="Layers.AnimationLayer.ClearLookTarget"/>
/// when you no longer need it (typically in <see cref="ScheduleBase.OnEnd"/>).
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

### `MoveTo.cs`

```csharp
using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that commands the NavigationLayer to move to a target position or GameObject.
/// When tracking a GameObject, re-evaluates the path periodically.
/// Does not override the NPC's look target — but will rotate the body to face the
/// movement direction when the angle would otherwise cause silly walking
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
			// Navigate to the closest point on the object's bounds, not its origin.
			// This prevents the NPC from trying to walk inside large props.
			var bounds = TargetObject.GetBounds();
			return bounds.ClosestPoint( Npc.WorldPosition );
		}

		return TargetPosition;
	}
}
```

## Разбор кода

### Wait — задача ожидания

Самая простая задача. Использует встроенный тип `TimeUntil` — обратный таймер.

```csharp
private TimeUntil _endTime;
```

`TimeUntil` — структура s&box, которая автоматически считает время до наступления момента. Приведение к `bool` возвращает `true`, когда время вышло.

**`OnStart`**: запускает таймер на `Duration` секунд.

**`OnUpdate`**: проверяет `_endTime` — если `true` (время вышло), возвращает `Success`. Иначе — `Running`.

**Пример использования**:
```csharp
new Wait( 3.0f ) // NPC ждёт 3 секунды
```

---

### LookAt — задача «посмотри на цель»

Поддерживает два режима: взгляд на точку (`Vector3`) или на объект (`GameObject`).

**Два конструктора**:
```csharp
public LookAt( Vector3 targetPosition )  // фиксированная точка
public LookAt( GameObject gameObject )    // динамический объект
```

**`OnStart`**: устанавливает цель взгляда через `Npc.Animation.SetLookTarget()`. Слой анимации начинает плавно поворачивать голову и тело NPC.

**`OnUpdate`**:
- Если объект-цель уничтожен и нет фиксированной позиции → `Failed`.
- `IsFacingTarget()` проверяет, довернулся ли NPC до цели → `Success`.
- Иначе → `Running` (NPC ещё поворачивается).

> **Важно**: цель взгляда **сохраняется** после завершения задачи! Если нужно сбросить, вызовите `Npc.Animation.ClearLookTarget()` в обработчике `OnEnd` расписания.

---

### MoveTo — задача перемещения

Самая сложная из базовых задач. Управляет навигацией NPC по NavMesh.

#### Параметры

| Свойство | По умолчанию | Описание |
|---|---|---|
| `StopDistance` | `10f` | Дистанция до цели, на которой NPC остановится |
| `ReevaluateInterval` | `0.5f` | Как часто пересчитывать путь для движущихся целей (сек) |
| `LateralThreshold` | `60f` | Угол (градусы), после которого NPC разворачивает тело |

#### `OnStart`

```csharp
Npc.Navigation.MoveTo( pos.Value, StopDistance );
```

Передаёт целевую позицию слою навигации. Тот строит путь по NavMesh.

#### `OnUpdate` — три основные секции

**1. Проверка уничтоженной цели**:
```csharp
if ( TargetObject is not null && !TargetObject.IsValid() )
    return TaskStatus.Failed;
```

Если целевой объект был удалён из сцены — задача проваливается.

**2. Пересчёт пути**:
```csharp
if ( TargetObject.IsValid() && _lastReevaluate > ReevaluateInterval )
```

Если цель — движущийся объект, путь пересчитывается каждые 0.5 секунды.

**3. Поворот тела при боковом движении**:
```csharp
var angle = Vector3.GetAngle( fwd, moveDir );
if ( angle > LateralThreshold && !Npc.Animation.LookTarget.HasValue )
```

Если NPC движется под большим углом к своему направлению взгляда (больше 60°) и у него нет заданной цели взгляда — плавно поворачивает тело в направлении движения. Это предотвращает «кривую» ходьбу боком.

#### `GetTargetPosition` — вычисление точки назначения

```csharp
var bounds = TargetObject.GetBounds();
return bounds.ClosestPoint( Npc.WorldPosition );
```

Для `GameObject` навигация идёт не к центру объекта, а к ближайшей точке на его границах. Это не даёт NPC пытаться «залезть внутрь» крупных объектов.

## Что проверить

1. Создайте NPC и добавьте задачу `Wait(5)` — NPC должен стоять ровно 5 секунд.
2. Добавьте `LookAt(playerObject)` — NPC должен повернуться к игроку и завершить задачу.
3. Проверьте, что после `LookAt` NPC продолжает смотреть на цель (цель сохраняется).
4. Добавьте `MoveTo(position)` — NPC должен дойти до точки и остановиться.
5. Добавьте `MoveTo(movingObject)` — NPC должен следовать за движущимся объектом.
6. Уничтожьте целевой объект во время `MoveTo` — задача должна вернуть `Failed`.
7. Проверьте, что NPC не ходит боком при резких поворотах маршрута.
