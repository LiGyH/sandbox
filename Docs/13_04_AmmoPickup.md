# 13.04 — Патроны (AmmoPickup) 🔋

## Что мы делаем?

Создаём **AmmoPickup** — подбираемый предмет, дающий запасные патроны для конкретного типа оружия.

## Создай файл

Путь: `Code/Items/Pickups/AmmoPickup.cs`

```csharp
/// <summary>
/// A pickup that gives the player reserve ammo for a matching weapon.
/// </summary>
public sealed class AmmoPickup : BasePickup
{
	/// <summary>
	/// The weapon prefab this ammo is for.
	/// </summary>
	[Property, Group( "Ammo" )] public GameObject WeaponPrefab { get; set; }

	/// <summary>
	/// The quantity of ammo to give.
	/// </summary>
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		if ( !WeaponPrefab.IsValid() ) return false;

		var weaponType = WeaponPrefab.GetComponent<BaseWeapon>( true )?.GetType();
		if ( weaponType is null ) return false;

		var existing = inventory.Weapons.OfType<BaseWeapon>().FirstOrDefault( x => x.GetType() == weaponType );
		if ( !existing.IsValid() ) return false;

		return existing.ReserveAmmo < existing.MaxReserveAmmo;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( !WeaponPrefab.IsValid() ) return false;

		var weaponType = WeaponPrefab.GetComponent<BaseWeapon>( true )?.GetType();
		if ( weaponType is null ) return false;

		var existing = inventory.Weapons.OfType<BaseWeapon>().FirstOrDefault( x => x.GetType() == weaponType );
		if ( !existing.IsValid() ) return false;

		existing.AddReserveAmmo( AmmoAmount );
		return true;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `WeaponPrefab` | Привязка к конкретному типу оружия (через префаб) |
| `GetComponent<BaseWeapon>(true)` | `true` = включая неактивные компоненты (в префабе) |
| `GetType() == weaponType` | Сравниваем типы — патроны для Shotgun подходят только к Shotgun |
| `ReserveAmmo < MaxReserveAmmo` | Нельзя подобрать если запас патронов полный |

---

Следующий шаг: [13.05 — Подбор инвентарных предметов (InventoryPickup)](13_05_InventoryPickup.md)
