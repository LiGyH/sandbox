# 03.14 — Наблюдатель после смерти (PlayerObserver) 👁️

## Что мы делаем?

Создаём **PlayerObserver** — компонент, который появляется после смерти игрока. Он позволяет камере вращаться вокруг трупа и ждёт нажатия кнопки для респавна.

## Как это работает?

### Жизненный цикл

1. Игрок умирает → `Player.CreateRagdollAndGhost()` создаёт `PlayerObserver`
2. Observer ищет `DeathCameraTarget` с таким же Connection
3. Камера вращается вокруг трупа
4. Игрок нажимает Attack1/Jump ИЛИ проходит 4 секунды → респавн
5. Observer уничтожается

### Камера вращения

```csharp
private void RotateAround( Component target )
{
    // Находим позицию головы трупа
    if ( !target.Components.Get<SkinnedModelRenderer>().TryGetBoneTransform( "head", out var tx ) )
    {
        tx.Position = target.GameObject.GetBounds().Center + Vector3.Up * 25f;
    }

    // Обновляем углы камеры
    e += Input.AnalogLook;
    e.pitch = e.pitch.Clamp( -90, 90 );

    // Позиция камеры = центр - назад * 150
    var targetPos = center - EyeAngles.Forward * 150f;

    // Трейс — не проходим сквозь стены
    var tr = Scene.Trace.FromTo( center, targetPos ).Radius( 1.0f ).WithoutTags( "ragdoll", "effect" ).Run();

    // Плавное перемещение камеры
    Scene.Camera.WorldPosition = Vector3.Lerp( Scene.Camera.WorldPosition, tr.EndPosition, timeSinceStarted, true );
}
```

### Защита от мгновенного респавна

```csharp
if ( timeSinceStarted < 1 ) return;  // первую секунду нельзя респавниться
```

Без этого игрок мог бы случайно нажать кнопку и сразу респавниться, не увидев свою смерть.

## Создай файл

Путь: `Code/Player/PlayerObserver.cs`

```csharp
/// <summary>
/// Dead players become these. They try to observe their last corpse. 
/// </summary>
public sealed class PlayerObserver : Component
{
	Angles EyeAngles;
	TimeSince timeSinceStarted;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		EyeAngles = Scene.Camera.WorldRotation;
		timeSinceStarted = 0;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		var corpse = Scene.GetAllComponents<DeathCameraTarget>()
					.Where( x => x.Connection == Network.Owner )
					.OrderByDescending( x => x.Created )
					.FirstOrDefault();

		if ( corpse.IsValid() )
		{
			RotateAround( corpse );
		}

		// Don't allow immediate respawn
		if ( timeSinceStarted < 1 )
			return;

		// If pressed a button, or has been too long
		if ( Input.Pressed( "attack1" ) || Input.Pressed( "jump" ) || timeSinceStarted > 4f )
		{
			PlayerData.For( Network.Owner )?.RequestRespawn();
			GameObject.Destroy();
		}
	}

	private void RotateAround( Component target )
	{
		// Find the corpse eyes
		if ( !target.Components.Get<SkinnedModelRenderer>().TryGetBoneTransform( "head", out var tx ) )
		{
			tx.Position = target.GameObject.GetBounds().Center + Vector3.Up * 25f;
		}

		var e = EyeAngles;
		e += Input.AnalogLook;
		e.pitch = e.pitch.Clamp( -90, 90 );
		e.roll = 0.0f;
		EyeAngles = e;

		var center = tx.Position;
		var targetPos = center - EyeAngles.Forward * 150f;

		var tr = Scene.Trace.FromTo( center, targetPos ).Radius( 1.0f ).WithoutTags( "ragdoll", "effect" ).Run();

		Scene.Camera.WorldPosition = Vector3.Lerp( Scene.Camera.WorldPosition, tr.EndPosition, timeSinceStarted, true );
		Scene.Camera.WorldRotation = EyeAngles;
	}
}
```

## Ключевые концепции

### Network.Owner и IsProxy

```csharp
if ( IsProxy ) return;
```

`IsProxy = true` означает: «этот объект принадлежит другому клиенту». Observer управляет камерой только для своего владельца.

### Два пути респавна

| Путь | Триггер | Механизм |
|------|---------|----------|
| Ручной | Нажатие кнопки | `PlayerData.RequestRespawn()` |
| Автоматический | 4 секунды | `timeSinceStarted > 4f` |

Оба пути вызывают `PlayerData.RequestRespawn()`, который создаёт нового игрока и уничтожает Observer.

### Vector3.Lerp с временем

```csharp
Vector3.Lerp( Scene.Camera.WorldPosition, tr.EndPosition, timeSinceStarted, true );
```

`timeSinceStarted` растёт от 0 → камера плавно перемещается к трупу (в начале = позиция глаз живого игрока, потом = позиция вращения вокруг трупа).

## Проверка

1. Умри → камера вращается вокруг тела
2. Можно двигать мышью → камера орбитально вращается
3. Нажми ЛКМ или Space → респавн
4. Подожди 4 секунды → автореспавн

## Следующий файл

Переходи к **03.15 — Статистика игрока (PlayerStats)**.
