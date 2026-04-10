# 03.15 — Статистика игрока (PlayerStats) 📈

## Что мы делаем?

Создаём компонент **PlayerStats** — записывает игровую статистику в Steam: пройденные метры, прыжки, полученный урон, смерти, суициды. Эти данные отображаются в профиле Steam.

## Как это работает?

### Подсчёт пройденного расстояния

```csharp
float metersTravelled;
Vector3 lastPosition;

protected override void OnFixedUpdate()
{
    var delta = WorldPosition - lastPosition;
    lastPosition = WorldPosition;

    if ( !Player.Controller.IsOnGround ) return;  // не считаем полёт

    var groundDelta = delta.WithZ( 0 ).Length.InchToMeter();
    if ( groundDelta > 10 ) groundDelta = 0;       // телепортация — не считаем

    metersTravelled += groundDelta;

    if ( metersTravelled > 10 )
    {
        Sandbox.Services.Stats.Increment( "meters_walked", metersTravelled );
        metersTravelled = 0;
    }
}
```

1. **Каждый физический тик** (`OnFixedUpdate`) считаем дельту
2. **Только на земле** — полёт и noclip не считаются
3. **WithZ(0)** — горизонтальное расстояние (без вертикали)
4. **InchToMeter()** — s&box работает в дюймах, Steam показывает метры
5. **Порог 10м** — отправляем не каждый кадр, а пачками по 10 метров
6. **Порог 10м/тик** — если дельта > 10 метров за тик, это телепортация (не считаем)

### События

```csharp
void IPlayerEvent.OnJump()
{
    Sandbox.Services.Stats.Increment( "jump", 1 );
}

void IPlayerEvent.OnDamage( IPlayerEvent.DamageParams args )
{
    Sandbox.Services.Stats.Increment( "damage_taken", args.Damage );
}

void IPlayerEvent.OnDied( IPlayerEvent.DiedParams args )
{
    Sandbox.Services.Stats.Increment( "deaths", 1 );
}

void IPlayerEvent.OnSuicide()
{
    Sandbox.Services.Stats.Increment( "suicides", 1 );
}
```

Каждое событие просто инкрементирует соответствующий счётчик в Steam.

## Создай файл

Путь: `Code/Player/PlayerStats.cs`

```csharp
/// <summary>
/// Record stats for the local player
/// </summary>
public sealed class PlayerStats : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	float metersTravelled;
	Vector3 lastPosition;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var delta = WorldPosition - lastPosition;
		lastPosition = WorldPosition;


		if ( !Player.Controller.IsOnGround )
		{
			return;
		}

		var groundDelta = delta.WithZ( 0 ).Length.InchToMeter();
		if ( groundDelta > 10 ) groundDelta = 0;

		metersTravelled += groundDelta;

		if ( metersTravelled > 10 )
		{
			Sandbox.Services.Stats.Increment( "meters_walked", metersTravelled );
			metersTravelled = 0;
		}

	}

	void IPlayerEvent.OnJump()
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "jump", 1 );
	}

	void IPlayerEvent.OnDamage( IPlayerEvent.DamageParams args )
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "damage_taken", args.Damage );
	}

	void IPlayerEvent.OnDied( IPlayerEvent.DiedParams args )
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "deaths", 1 );
	}

	void IPlayerEvent.OnSuicide()
	{
		if ( IsProxy ) return;

		Sandbox.Services.Stats.Increment( "suicides", 1 );
	}

}
```

## Ключевые концепции

### IsProxy — только для себя

```csharp
if ( IsProxy ) return;
```

Статистику записывает только **сам игрок** (`!IsProxy`). Серверу не нужно отправлять чужую статистику — каждый клиент ведёт свою.

### OnFixedUpdate vs OnUpdate

`OnFixedUpdate` вызывается с фиксированной частотой (обычно 60 Гц), независимо от FPS. Это даёт стабильный подсчёт расстояния — на 30 FPS и на 240 FPS результат одинаковый.

### Sandbox.Services.Stats

API для Steam-статистики. `Increment` добавляет значение к счётчику. Статистики определяются в настройках проекта в Steamworks.

## Проверка

1. Пробежись по карте → `meters_walked` увеличивается
2. Прыгни → `jump` +1
3. Умри → `deaths` +1

## Следующий файл

Переходи к **03.16 — Голосовой чат (SandboxVoice)**.
