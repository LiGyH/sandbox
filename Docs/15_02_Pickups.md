# 15.02 — Базовый подбираемый предмет (BasePickup) 📦

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

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

Следующий шаг: [13.03 — Аптечка (HealthPickup)](15_02_Pickups.md)


---

# 13.03 — Аптечка (HealthPickup) ❤️

## Что мы делаем?

Создаём **HealthPickup** — подбираемый предмет, восстанавливающий здоровье игрока.

## Создай файл

Путь: `Code/Items/Pickups/HealthPickup.cs`

```csharp
﻿/// <summary>
/// A pickup that gives the player some health.
/// </summary>
public sealed class HealthPickup : BasePickup
{
	/// <summary>
	/// How much health to give to the player
	/// </summary>
	[Property, Group( "Health" )] float HealthGive { get; set; } = 0;

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		if ( player.Health >= player.MaxHealth && HealthGive > 0 )
			return false;

		return true;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		player.Health = (player.Health + HealthGive).Clamp( 0, player.MaxHealth );
		player.PlayerData.AddStat( $"pickup.health" );

		return true;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `CanPickup` | Нельзя подобрать если здоровье уже максимальное |
| `Clamp(0, MaxHealth)` | Здоровье не может превысить максимум и не может стать отрицательным |
| `AddStat("pickup.health")` | Статистика подборов для PlayerData |

---

Следующий шаг: [13.04 — Патроны (AmmoPickup)](15_02_Pickups.md)


---

# 13.04 — Патроны (AmmoPickup) 🔋

## Что мы делаем?

Создаём **AmmoPickup** — подбираемый предмет, дающий запасные патроны в общий пул патронов игрока по заданному [`AmmoResource`](06_10_AmmoResource_Inventory.md). Теперь пикап привязан не к конкретному оружию, а к **типу патронов**: одна коробка 9мм подходит всем 9мм-пушкам, которые ссылаются на тот же `AmmoResource`.

## Создай файл

Путь: `Code/Items/Pickups/AmmoPickup.cs`

```csharp
/// <summary>
/// A pickup that adds ammo to the player's shared pool for a given AmmoResource.
/// </summary>
public sealed class AmmoPickup : BasePickup
{
	/// <summary>
	/// The ammo resource this pickup gives ammo for.
	/// When set, ammo is added directly to the player's shared pool for that resource.
	/// </summary>
	[Property, Group( "Ammo" )] public AmmoResource AmmoType { get; set; }

	/// <summary>
	/// The quantity of ammo to give.
	/// </summary>
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is not null )
		{
			var ammoInv = player.GetComponent<AmmoInventory>();
			if ( ammoInv is null ) return false;
			return ammoInv.GetAmmo( AmmoType ) < AmmoType.MaxReserve;
		}

		return false;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is not null )
		{
			var ammoInv = player.GetComponent<AmmoInventory>();
			if ( ammoInv is null ) return false;
			return ammoInv.AddAmmo( AmmoType, AmmoAmount ) > 0;
		}

		return true;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `AmmoType` | `AmmoResource` — ссылка на общий тип патронов (см. [06.10](06_10_AmmoResource_Inventory.md)). Без него подбор невозможен. |
| `player.GetComponent<AmmoInventory>()` | Получаем общий пул патронов игрока. |
| `ammoInv.GetAmmo(AmmoType) < AmmoType.MaxReserve` | Запрещаем подбор, когда пул уже полон. |
| `ammoInv.AddAmmo(AmmoType, AmmoAmount)` | Кладём патроны в общий пул. Возвращает фактически добавленное количество; если 0 — пикап не считается успешным. |

> ⚠️ **Миграция**: старое поле `WeaponPrefab` (ссылка на префаб оружия) удалено. Теперь пикап описывается только типом патронов. Обнови префабы пикапов `ammo_pistol`, `ammo_rifle`, `ammo_shotgun`, `ammo_rocket` — присвой им соответствующий `.ammo`-ресурс (`Assets/ammotype/9mm.ammo`, `rifle.ammo`, `shotgun.ammo`, `rocket.ammo`).

---

Следующий шаг: [13.05 — Подбор инвентарных предметов (InventoryPickup)](15_03_InventoryPickup.md)

---

<!-- seealso -->
## 🔗 См. также

- [06.04 — BaseWeapon](06_04_BaseWeapon.md)
- [03.08 — PlayerInventory](03_08_PlayerInventory.md)

