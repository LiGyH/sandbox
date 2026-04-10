# 13.02 — Базовый подбираемый предмет (BasePickup) 📦

## Что мы делаем?

Создаём **BasePickup** — абстрактный базовый класс для всех подбираемых предметов (аптечки, патроны, оружие).

## Как это работает?

`BasePickup` реагирует на два типа событий:
- **ITriggerListener** — когда игрок входит в триггер-зону (автоматический подбор)
- **ICollisionListener** — когда игрок физически касается предмета (подбор при столкновении)

В обоих случаях:
1. Проверяем, что мы на хосте
2. Находим компонент `Player` и `PlayerInventory`
3. Вызываем `CanPickup` (можно ли подобрать?) и `OnPickup` (применяем эффект)
4. Играем звуковой эффект
5. Уничтожаем предмет

## Создай файл

Путь: `Code/Items/Pickups/BasePickup.cs`

```csharp
/// <summary>
/// A weapon, or weapons, or ammo, that can be picked up
/// </summary>
public abstract class BasePickup : Component, Component.ITriggerListener, Component.ICollisionListener
{
	/// <summary>
	/// The pickup's collider, it is required
	/// </summary>
	[RequireComponent] public Collider Collider { get; set; }

	/// <summary>
	/// The sound to play when picking up this item
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	/// <summary>
	/// Check if the player can pick up this object
	/// </summary>
	public virtual bool CanPickup( Player player, PlayerInventory inventory )
	{
		return true;
	}

	/// <summary>
	/// Give the player the effect of this pickup
	/// </summary>
	/// <returns>Should this object be consumed, eg on successful pickup</returns>
	protected virtual bool OnPickup( Player player, PlayerInventory inventory )
	{
		return true;
	}

	/// <summary>
	/// Called when a gameobject enters the trigger.
	/// </summary>
	void ITriggerListener.OnTriggerEnter( GameObject other )
	{
		if ( !Networking.IsHost ) return;
		if ( GameObject.IsDestroyed ) return;

		if ( !other.Components.TryGet( out Player player ) )
			return;

		if ( !player.Components.TryGet( out PlayerInventory inventory ) )
			return;

		if ( !CanPickup( player, inventory ) )
			return;

		if ( !OnPickup( player, inventory ) )
			return;

		PlayPickupEffects( player );
		DestroyGameObject();
	}

	/// <summary>
	/// Called when a gameobject enters the trigger.
	/// </summary>
	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost ) return;
		if ( GameObject.IsDestroyed ) return;

		if ( !collision.Other.GameObject.Root.Components.TryGet( out Player player ) )
			return;

		if ( !player.Components.TryGet( out PlayerInventory inventory ) )
			return;

		if ( !CanPickup( player, inventory ) )
			return;

		if ( !OnPickup( player, inventory ) )
			return;

		PlayPickupEffects( player );
		DestroyGameObject();
	}

	[Rpc.Broadcast]
	protected void PlayPickupEffects( Player player )
	{
		if ( Application.IsDedicatedServer ) return;

		var snd = GameObject.PlaySound( PickupSound );
		if ( !snd.IsValid() )
			return;

		if ( player.IsValid() && player.IsLocalPlayer )
		{
			snd.SpacialBlend = 0;
		}
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `[RequireComponent] Collider` | Движок автоматически добавит коллайдер если его нет |
| `ITriggerListener` | Срабатывает когда другой объект входит в триггер |
| `ICollisionListener` | Срабатывает при физическом столкновении |
| `Networking.IsHost` | Подбор обрабатывается только на сервере |
| `[Rpc.Broadcast]` | Звук подбора играется на всех клиентах |
| `SpacialBlend = 0` | Для локального игрока звук без позиционирования (2D) |

## Результат

- Базовый класс для создания любых подбираемых предметов
- Поддержка триггеров и физических столкновений
- Звуковые эффекты при подборе

---

Следующий шаг: [13.03 — Аптечка (HealthPickup)](13_03_HealthPickup.md)
