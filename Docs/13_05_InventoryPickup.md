# 13.05 — Подбор инвентарных предметов (InventoryPickup) 🎒

## Что мы делаем?

Создаём **InventoryPickup** — предмет, содержащий одно или несколько оружий/предметов, которые добавляются в инвентарь при подборе. В отличие от `DroppedWeapon`, этот можно подобрать ещё и нажатием E.

## Создай файл

Путь: `Code/Items/Pickups/InventoryPickup.cs`

```csharp
﻿/// <summary>
/// A pickup that gives an inventory item, like a weapon
/// </summary>
public sealed class InventoryPickup : BasePickup, Component.IPressable
{
	/// <summary>
	/// A list of prefabs (that have to be inventory items) that are given to the player
	/// </summary>
	[Property, Group( "Inventory" )] public List<GameObject> Items { get; set; }

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( Items == null || Items.Count == 0 ) return null;
		return new IPressable.Tooltip( "Pick up", "inventory_2", string.Join( ", ", Items.Select( i => (i.GetComponent<BaseCarryable>()?.DisplayName ?? i.Name).ToUpper() ) ) );
	}

	public bool Press( IPressable.Event e )
	{
		DoPickup( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void DoPickup( GameObject presserObject )
	{
		// Already got deleted, or something 
		if ( !presserObject.IsValid() ) return;

		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() ) return;

		if ( OnPickup( player, player.GetComponent<PlayerInventory>() ) )
		{
			PlayPickupEffects( player );
			GameObject.Destroy();
		}
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( Items == null ) return false;

		bool consumed = false;
		foreach ( var prefab in Items )
		{
			if ( inventory.Pickup( prefab ) )
			{
				consumed = true;
				player.PlayerData.AddStat( $"pickup.inventory.{prefab.Name}" );
			}
		}

		return consumed;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `List<GameObject> Items` | Список префабов предметов (можно несколько) |
| `IPressable` | Добавляет подбор нажатием E (в дополнение к триггеру/коллизии из BasePickup) |
| `GetTooltip` | Показывает имена всех предметов через запятую |
| `inventory.Pickup(prefab)` | Метод инвентаря — клонирует префаб и добавляет в инвентарь |
| `consumed = true` | Если хоть один предмет подобран — уничтожаем пикап |

## Результат

Фаза 13 завершена! Все подбираемые предметы документированы:
- `DroppedWeapon` — оружие на земле
- `BasePickup` — базовый класс
- `HealthPickup` — аптечка
- `AmmoPickup` — патроны
- `InventoryPickup` — оружие/предметы

---

Следующий шаг: [14.01 — NPC: Основной класс (Npc)](14_01_Npc.md)
