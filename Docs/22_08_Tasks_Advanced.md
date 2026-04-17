# 22_08 — Продвинутые задачи NPC (Say, FireWeapon, PickUpProp, DropProp)

## Что мы делаем?

Создаём четыре продвинутые задачи для NPC, которые добавляют осмысленное поведение:

- **Say** — NPC произносит фразу (звуковой файл или текстовое сообщение с субтитрами).
- **FireWeapon** — NPC стреляет из оружия по цели в течение заданного времени.
- **PickUpProp** — NPC берёт предмет в руки.
- **DropProp** — NPC бросает предмет, который держит.

## Как это работает внутри движка

Все задачи наследуют `TaskBase` (см. главу 22_07). Дополнительно здесь используются слои NPC:

- `Npc.Speech` (`SpeechLayer`) — слой речи. Метод `Say()` начинает воспроизведение звука и показывает субтитры. Свойство `IsSpeaking` сигнализирует о завершении.
- `Npc.Animation` (`AnimationLayer`) — слой анимации. Методы `SetHeldProp()` / `ClearHeldProp()` управляют удержанием предмета, `TriggerAttack()` запускает анимацию атаки, `IsFacingTarget()` проверяет направление взгляда.
- `BaseWeapon` — базовый класс оружия с методами `CanPrimaryAttack()` и `PrimaryAttack()`.

## Путь к файлу

```
Code/Npcs/Tasks/Say.cs
Code/Npcs/Tasks/FireWeapon.cs
Code/Npcs/Tasks/PickUpProp.cs
Code/Npcs/Tasks/DropProp.cs
```

## Полный код

### `Say.cs`

```csharp
using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Task that plays speech via the SpeechLayer. Waits for the speech to finish before completing.
/// Accepts either a SoundEvent or a plain string (which uses the fallback sound).
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

### `FireWeapon.cs`

```csharp
using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs.Tasks;

/// <summary>
/// Shoots a weapon at a target for a specific duration
/// </summary>
public class FireWeapon : TaskBase
{
	/// <summary>The weapon component to fire.</summary>
	public BaseWeapon Weapon { get; }

	/// <summary>The GameObject to aim at.</summary>
	public GameObject Target { get; }

	/// <summary>How long (seconds) to keep firing before the task completes.</summary>
	public float BurstDuration { get; }

	/// <summary>Body rotation speed (degrees/s scale) used while actively aiming. Higher than the default look speed.</summary>
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
		if ( !Weapon.IsValid() )
			return TaskStatus.Failed;

		if ( !Target.IsValid() )
			return TaskStatus.Failed;

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

### `PickUpProp.cs`

```csharp
namespace Sandbox.Npcs.Tasks;

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

### `DropProp.cs`

```csharp
namespace Sandbox.Npcs.Tasks;

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

## Разбор кода

### Say — задача «произнеси фразу»

#### Два режима использования

**Режим 1: Звуковой файл** (`SoundEvent`)
```csharp
new Say( mySoundEvent )          // длительность определяется из звукового файла
new Say( mySoundEvent, 5f )      // принудительная длительность 5 секунд
```

**Режим 2: Текстовое сообщение**
```csharp
new Say( "Привет, путник!", 3f ) // текст с субтитрами на 3 секунды
```

При текстовом режиме используется «запасной» звук (fallback) из слоя речи.

#### Логика

**`OnStart`**: выбирает подходящий метод `speech.Say()` в зависимости от того, передан `SoundEvent` или строка.

**`OnUpdate`**: проверяет `Npc.Speech.IsSpeaking`. Пока NPC говорит — `Running`. Когда фраза закончилась — `Success`.

---

### FireWeapon — задача «стрелять по цели»

Наиболее сложная задача в этом наборе.

#### Свойства

| Свойство | Тип | По умолчанию | Описание |
|---|---|---|---|
| `Weapon` | `BaseWeapon` | — | Компонент оружия (обязательно) |
| `Target` | `GameObject` | — | Объект-цель (обязательно) |
| `BurstDuration` | `float` | `1.5f` | Длительность очереди в секундах |
| `AimTurnSpeed` | `float` | `8f` | Скорость поворота тела при прицеливании |

#### `OnStart`

```csharp
_burstEnd = BurstDuration;
```

Запускает таймер `TimeUntil` на длительность очереди.

#### `OnUpdate` — пошаговая логика

1. **Проверка валидности**: если оружие или цель уничтожены — `Failed`.

2. **Поворот к цели**: `RotateBodyTowardTarget()` — плавно поворачивает тело NPC в направлении цели:
   ```csharp
   var toTarget = (Target.WorldPosition - Npc.WorldPosition).WithZ( 0 );
   ```
   `WithZ(0)` — убирает вертикальную составляющую, чтобы NPC поворачивался только по горизонтали.
   
   `Rotation.Lerp` — плавная интерполяция поворота со скоростью `AimTurnSpeed`.

3. **Стрельба**: NPC стреляет **только когда смотрит на цель** и оружие готово:
   ```csharp
   if ( Npc.Animation.IsFacingTarget() && Weapon.CanPrimaryAttack() )
   ```
   `CanPrimaryAttack()` учитывает скорострельность и перезарядку.

4. **Завершение**: когда `_burstEnd` становится `true` (время вышло) — `Success`.

---

### PickUpProp — задача «подними предмет»

Мгновенная задача (завершается за один кадр).

```csharp
protected override void OnStart()
{
    Npc.Animation.SetHeldProp( Target );
}

protected override TaskStatus OnUpdate()
{
    return TaskStatus.Success;
}
```

`SetHeldProp` прикрепляет указанный `GameObject` к руке NPC через слой анимации. Задача сразу возвращает `Success`, потому что привязка происходит мгновенно.

**Пример использования**:
```csharp
new PickUpProp( boxObject ) // NPC берёт коробку в руку
```

---

### DropProp — задача «брось предмет»

Зеркальная к `PickUpProp`. Тоже мгновенная.

```csharp
protected override void OnStart()
{
    Npc.Animation.ClearHeldProp();
}
```

`ClearHeldProp` отсоединяет предмет от руки NPC. Обратите внимание: конструктор принимает `target`, но `OnStart` игнорирует его — `ClearHeldProp` сбрасывает **любой** удерживаемый предмет.

**Пример цепочки**:
```csharp
new MoveTo( boxPosition ),
new PickUpProp( box ),
new MoveTo( dropZone ),
new DropProp( box )
```

NPC подходит к коробке, берёт её, несёт к зоне сброса и бросает.

## Что проверить

1. **Say с SoundEvent**: NPC должен воспроизвести звук, рот должен шевелиться, субтитры должны появиться.
2. **Say с текстом**: NPC должен показать субтитры на экране в течение заданного времени.
3. **FireWeapon**: NPC должен повернуться к цели и начать стрелять. Проверьте, что стрельба начинается только после поворота.
4. **FireWeapon с уничтоженной целью**: задача должна вернуть `Failed`.
5. **PickUpProp**: предмет должен «прилипнуть» к руке NPC.
6. **DropProp**: предмет должен отсоединиться и упасть (с учётом физики).
7. **Цепочка задач**: `MoveTo → PickUpProp → MoveTo → DropProp` — полный цикл переноса предмета.


---

## ➡️ Следующий шаг

Переходи к **[22.09 — NPC: Боевой NPC (CombatNpc) ⚔️](22_09_CombatNpc.md)**.
