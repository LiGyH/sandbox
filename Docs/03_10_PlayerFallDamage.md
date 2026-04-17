# 03.10 — Урон от падения (PlayerFallDamage) 🦴

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём компонент **урона от падения** — если игрок падает с большой высоты, он получает урон пропорционально скорости падения. Этот компонент слушает событие `IPlayerEvent.OnLand` и применяет урон.

## Как это работает?

### Формула урона

```
Урон = Remap(скорость_падения, SafeSpeed → FatalSpeed, 0 → 100) × DamageMultiplier
```

- **MaxSafeFallSpeed = 512** — ниже этой скорости урона нет
- **FatalFallSpeed = 1536** — при этой скорости или выше — мгновенная смерть (100 HP)
- **DamageMultiplier = 1.0** — можно уменьшить/увеличить на уровне сцены

Пример: скорость = 1024 (середина) → урон ≈ 50 HP.

### MathX.Remap

```csharp
var damageAmount = MathX.Remap( fallSpeed, MaxSafeFallSpeed, FatalFallSpeed, 0f, 100f );
```

`Remap` — линейная интерполяция из одного диапазона в другой:
- Вход: [512, 1536]
- Выход: [0, 100]
- Значение 512 → 0, значение 1536 → 100

### Сетевой вызов

```csharp
[Rpc.Broadcast]
public void TakeFallDamage( float amount )
{
    if ( !Networking.IsHost ) return;
    // ...
}
```

Метод помечен `[Rpc.Broadcast]`, но реальный урон применяется **только на хосте** (`if ( !Networking.IsHost ) return`). Зачем broadcast? Чтобы анимация и звук воспроизвелись на **всех клиентах**.

## Создай файл

Путь: `Code/Player/PlayerFallDamage.cs`

```csharp
/// <summary>
/// Apply fall damage to the player
/// </summary>
public class PlayerFallDamage : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// Fatal fall speed, you will die if you fall at or above this speed
	/// </summary>
	[Property] public float FatalFallSpeed { get; set; } = 1536.0f;

	/// <summary>
	/// Maximum safe fall speed, you won't take damage at or below this speed
	/// </summary>
	[Property] public float MaxSafeFallSpeed { get; set; } = 512.0f;

	/// <summary>
	/// Multiply damage amount by this much
	/// </summary>
	[Property] public float DamageMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// Fall damage sound
	/// </summary>
	[Property] public SoundEvent FallSound { get; set; }

	[Rpc.Owner]
	private void PlayFallSound()
	{
		GameObject.PlaySound( FallSound );
	}

	void IPlayerEvent.OnLand( float distance, Vector3 velocity )
	{
		var fallSpeed = Math.Abs( velocity.z );

		if ( fallSpeed <= MaxSafeFallSpeed )
			return;

		var damageAmount = MathX.Remap( fallSpeed, MaxSafeFallSpeed, FatalFallSpeed, 0f, 100f ) * DamageMultiplier;
		if ( damageAmount < 1 ) return;

		if ( damageAmount >= Player.Health )
			Player.PlayerData?.AddStat( "player.fall.death" );

		TakeFallDamage( damageAmount );
	}


	[Rpc.Broadcast]
	public void TakeFallDamage( float amount )
	{
		if ( !Networking.IsHost ) return;


		if ( Player is IDamageable damage )
		{
			var dmg = new DamageInfo( amount.CeilToInt(), Player.GameObject, null );
			dmg.Tags.Add( DamageTags.Fall );
			damage.OnDamage( dmg );

			PlayFallSound();
		}
	}
}
```

## Ключевые концепции

### IPlayerEvent.OnLand

Это событие вызывается когда `PlayerController` обнаруживает, что игрок коснулся земли после падения. Параметры:
- `distance` — пройденное расстояние (не используется здесь)
- `velocity` — скорость в момент приземления (`velocity.z` — вертикальная составляющая)

### DamageTags.Fall

```csharp
dmg.Tags.Add( DamageTags.Fall );
```

Тег урона (из 02.04) — помечает, что это урон от падения. Другие системы могут использовать этот тег (например, лента убийств покажет иконку падения).

### Статистика смерти от падения

```csharp
if ( damageAmount >= Player.Health )
    Player.PlayerData?.AddStat( "player.fall.death" );
```

Если урон убьёт игрока — записываем в Steam-статистику. Заметь: `AddStat` вызывается **до** `TakeFallDamage`, потому что после смерти `Player` уже уничтожен.

## Проверка

1. Забрейся на высоту → прыгни вниз → получишь урон
2. Прыгни с небольшой высоты → урона нет
3. Прыгни с очень большой высоты → мгновенная смерть

## Следующий файл

Переходи к **03.10 — Фонарик игрока (PlayerFlashlight)**.
