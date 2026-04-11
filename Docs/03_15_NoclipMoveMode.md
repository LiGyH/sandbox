# 03.07 — Режим Noclip (NoclipMoveMode) 👻

## Что мы делаем?

Создаём **режим полёта Noclip** — когда игрок может летать сквозь стены (или без прохождения, если `EnableCollision = true`). Включается двойным прыжком.

## Как это работает?

### Наследование от MoveMode

```
MoveMode (базовый класс s&box)
  └── NoclipMoveMode
```

`MoveMode` — это абстрактный класс в движке. `PlayerController` проверяет все MoveMode-компоненты на игроке и активирует тот, у которого наивысший `Score`. У Noclip `Score = 1000` — это перебивает обычную ходьбу.

### Приоритет через Score

```csharp
public override int Score( PlayerController controller )
{
    return 1000;  // Выше чем у WalkMoveMode (~100)
}
```

Когда Noclip **включён** (`Enabled = true`), его Score = 1000 → PlayerController использует его. Когда **выключен**, компонент disabled → Score не проверяется.

### Движение в Noclip

```csharp
public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
{
    var direction = eyes * input;           // направление = взгляд × ввод
    if ( Input.Down( "jump" ) ) direction += Vector3.Up;    // пробел = вверх
    if ( Input.Down( "duck" ) ) direction += Vector3.Down;  // присед = вниз
    return direction * velocity;
}
```

- Направление берётся из **камеры** (куда смотришь — туда летишь)
- Скорость: 1200 при беге, 200 при ходьбе (Alt)
- Нет гравитации: `body.Gravity = false`
- Линейное торможение: `body.LinearDamping = 5.0f` — плавная остановка

### Анимация

```csharp
protected override void OnUpdateAnimatorState( SkinnedModelRenderer renderer )
{
    renderer.Set( "b_noclip", true );
    renderer.Set( "duck", 0f );
}
```

Специальная анимация для Noclip — модель игрока выглядит «парящей». `duck = 0` предотвращает анимацию приседания.

## Создай файл

Путь: `Code/Player/NoclipMoveMode.cs`

```csharp
using Sandbox.Movement;

public sealed class NoclipMoveMode : Sandbox.Movement.MoveMode
{
	/// <summary>
	/// If true, the player will still collide with the world and other players. This probably
	/// means that the noclip mode is named wrong. But it's cool. It just becomes a fly around mode.
	/// </summary>
	[Property]
	public bool EnableCollision { get; set; }

	[Property]
	public float RunSpeed { get; set; } = 1200;

	[Property]
	public float WalkSpeed { get; set; } = 200;


	protected override void OnUpdateAnimatorState( SkinnedModelRenderer renderer )
	{
		renderer.Set( "b_noclip", true );
		renderer.Set( "duck", 0f );
	}

	public override int Score( PlayerController controller )
	{
		return 1000;
	}

	public override void UpdateRigidBody( Rigidbody body )
	{
		body.Gravity = false;
		body.LinearDamping = 5.0f;
		body.AngularDamping = 1f;

		body.Tags.Set( "noclip", !EnableCollision );
	}

	public override void OnModeBegin()
	{
		Controller.IsClimbing = true;
		Controller.Body.Gravity = false;

		if ( !IsProxy )
			Sandbox.Services.Stats.Increment( "move.noclip.use", 1 );
	}

	public override void OnModeEnd( MoveMode next )
	{
		Controller.IsClimbing = false;
		Controller.Body.Velocity = Controller.Body.Velocity.ClampLength( Controller.RunSpeed );
		Controller.Body.Tags.Set( "noclip", false );
		Controller.Renderer.Set( "b_noclip", false );
	}

	public override Transform CalculateEyeTransform()
	{
		var transform = base.CalculateEyeTransform();

		// Undo the camera lowering that IsDucking causes
		if ( Controller.IsDucking )
			transform.Position += Vector3.Up * (Controller.BodyHeight - Controller.DuckedHeight);

		return transform;
	}

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
		// don't normalize, because analog input might want to go slow
		input = input.ClampLength( 1 );

		var direction = eyes * input;

		// Run if we're holding down alt move button
		bool run = Input.Down( Controller.AltMoveButton );

		// if Run is default, flip that logic
		if ( Controller.RunByDefault ) run = !run;

		// if we're running, use run speed, if not use walk speed
		var velocity = run ? RunSpeed * 2.0f : RunSpeed;

		// Slow down when the walk modifier (Alt) is held
		if ( Input.Down( "walk" ) ) velocity = WalkSpeed;

		if ( direction.IsNearlyZero( 0.1f ) )
		{
			direction = 0;
		}

		// if we're hold down jump move upwards
		if ( Input.Down( "jump" ) ) direction += Vector3.Up;

		// if we're hold down duck move downwards
		if ( Input.Down( "duck" ) ) direction += Vector3.Down;

		return direction * velocity;
	}

}
```

## Ключевые концепции

### MoveMode — система передвижения s&box

s&box использует **композицию режимов движения**. На одном игроке может быть несколько MoveMode (Walk, Swim, Noclip, Ladder), и `PlayerController` выбирает один с наивысшим Score.

### Tags и столкновения

```csharp
body.Tags.Set( "noclip", !EnableCollision );
```

Тег `"noclip"` настроен в матрице столкновений (00.05) так, что объекты с ним не сталкиваются с миром. Если `EnableCollision = true`, тег не ставится → столкновения работают → получается «fly mode» (без прохождения стен).

### Статистика

```csharp
Sandbox.Services.Stats.Increment( "move.noclip.use", 1 );
```

Каждое включение Noclip записывается в статистику Steam. Работает только для реальных игроков (`!IsProxy`).

## Проверка

1. Двойной прыжок → включается Noclip
2. WASD + мышь → летаешь в направлении взгляда
3. Пробел → вверх, Ctrl → вниз
4. Двойной прыжок ещё раз → выключается

## Следующий файл

Переходи к **03.08 — Данные игрока (PlayerData)**.
