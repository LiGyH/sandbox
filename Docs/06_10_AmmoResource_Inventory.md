# 06.10 — Общий пул патронов (AmmoResource + AmmoInventory) 🎯

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём два файла, реализующих **общий пул патронов** на игрока:

- `AmmoResource.cs` — `GameResource`, описывающий тип патронов (9мм, rifle, shotgun, rocket…).
- `AmmoInventory.cs` — компонент на игроке, хранящий словарь «тип патронов → количество».

Это заменяет старую модель «каждое оружие хранит свой резерв». Теперь, например, пистолет Glock и SMG MP5 могут ссылаться на один и тот же `9mm.ammo` — и делить общий склад патронов. Коробки `AmmoPickup` тоже привязаны к типу, а не к конкретной пушке.

## Зачем это нужно?

- **Общий резерв**: типичный шутер — пистолет-патроны, винтовочные, дробь, ракеты. Делают игру предсказуемой и понятной.
- **Hot-swap оружия**: подбирая третий AR на том же патроне, игрок не получает три независимых склада.
- **Ресурсы вместо магических строк**: `AmmoResource` задаётся в редакторе (`Category = "Sandbox"`, `Extension = "ammo"`), легко добавить новый тип.
- **Host-authoritative**: пул синхронизируется `[Sync( SyncFlags.FromHost )]`, а клиентские изменения проходят через `[Rpc.Host]`.

## Как это работает внутри движка?

### `AmmoResource`

| Поле | Описание |
|---|---|
| `Title` | Отображаемое имя типа патрона (для UI/HUD). |
| `Icon` | Опциональная текстура-иконка. |
| `MaxReserve` | Потолок резерва (по умолчанию `120`). |
| `DefaultStartingAmmo` | Сколько патронов «подсеять» в пул, когда игрок впервые получает оружие этого типа. |

### `AmmoInventory`

| Элемент | Описание |
|---|---|
| `Pool` | `NetDictionary<string, int>`, ключ — `resource.ResourcePath`, значение — текущий остаток. `[Sync( SyncFlags.FromHost )]`. |
| `GetAmmo(resource)` | Возвращает текущее количество патронов (0, если пула нет). |
| `SetAmmo(resource, value)` | На хосте — прямое присвоение с зажатием `[0, MaxReserve]`. На клиенте — вызывает `SetAmmoRpc` на хосте. |
| `AddAmmo(resource, count)` | Прибавляет патроны с учётом `MaxReserve`. Возвращает фактически добавленное. Клиент делает оптимистичный вызов. |
| `TakeAmmo(resource, count)` | Снимает `count` патронов, если они есть. Возвращает `true` при успехе. На клиенте — оптимистично + RPC. |
| `HasAmmo(resource, count = 1)` | Проверка. |
| `SetAmmoRpc / AddAmmoRpc / TakeAmmoRpc` | `[Rpc.Host]`-методы, выполняющие авторитетную запись в `Pool`. |

## Создай файл

**Путь:** `Code/Game/Weapon/AmmoResource.cs`

```csharp
/// <summary>
/// Defines a type of ammo that can be shared across weapons.
/// Weapons that reference the same AmmoResource draw from the same reserve pool on the player.
/// </summary>
[AssetType( Name = "Ammo Type", Extension = "ammo", Category = "Sandbox" )]
public class AmmoResource : GameResource
{
	/// <summary>
	/// Display name for this ammo type.
	/// </summary>
	[Property] public string Title { get; set; }

	/// <summary>
	/// Optional icon displayed in HUD and inventory.
	/// </summary>
	[Property] public Texture Icon { get; set; }

	/// <summary>
	/// Maximum reserve ammo a player can hold for this type.
	/// </summary>
	[Property] public int MaxReserve { get; set; } = 120;

	/// <summary>
	/// How much reserve ammo is seeded into the shared pool the first time
	/// a weapon of this ammo type is picked up (i.e. when the pool doesn't
	/// exist yet on the player).
	/// </summary>
	[Property] public int DefaultStartingAmmo { get; set; } = 0;
}
```

**Путь:** `Code/Game/Weapon/AmmoInventory.cs`

```csharp
/// <summary>
/// Stores shared ammo pools on a player, keyed by <see cref="AmmoResource"/>.
/// Add this component to the player prefab alongside <see cref="PlayerInventory"/>.
/// </summary>
public sealed class AmmoInventory : Component
{
	/// <summary>
	/// Ammo pool: resource path → current count.
	/// Host-authoritative so server-side pickups replicate correctly to the owning client.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<string, int> Pool { get; set; } = new();

	public int GetAmmo( AmmoResource resource )
	{
		if ( resource is null ) return 0;
		return Pool.TryGetValue( resource.ResourcePath, out var count ) ? count : 0;
	}

	public void SetAmmo( AmmoResource resource, int value )
	{
		if ( resource is null ) return;
		if ( !Networking.IsHost ) { SetAmmoRpc( resource, Math.Clamp( value, 0, resource.MaxReserve ) ); return; }
		Pool[resource.ResourcePath] = Math.Clamp( value, 0, resource.MaxReserve );
	}

	public int AddAmmo( AmmoResource resource, int count )
	{
		if ( resource is null ) return 0;
		if ( !Networking.IsHost ) { AddAmmoRpc( resource, count ); return count; }
		var current = GetAmmo( resource );
		var space = resource.MaxReserve - current;
		var toAdd = Math.Min( count, space );
		if ( toAdd <= 0 ) return 0;
		Pool[resource.ResourcePath] = current + toAdd;
		return toAdd;
	}

	public bool TakeAmmo( AmmoResource resource, int count )
	{
		if ( resource is null ) return false;
		if ( !Networking.IsHost ) { TakeAmmoRpc( resource, count ); return GetAmmo( resource ) >= count; }
		var current = GetAmmo( resource );
		if ( current < count ) return false;
		Pool[resource.ResourcePath] = current - count;
		return true;
	}

	public bool HasAmmo( AmmoResource resource, int count = 1 )
	{
		return GetAmmo( resource ) >= count;
	}

	[Rpc.Host]
	private void SetAmmoRpc( AmmoResource resource, int value )
	{
		Pool[resource.ResourcePath] = value;
	}

	[Rpc.Host]
	private void AddAmmoRpc( AmmoResource resource, int count )
	{
		var current = Pool.TryGetValue( resource.ResourcePath, out var c ) ? c : 0;
		var toAdd = Math.Min( count, resource.MaxReserve - current );
		if ( toAdd > 0 )
			Pool[resource.ResourcePath] = current + toAdd;
	}

	[Rpc.Host]
	private void TakeAmmoRpc( AmmoResource resource, int count )
	{
		var current = Pool.TryGetValue( resource.ResourcePath, out var c ) ? c : 0;
		if ( current >= count )
			Pool[resource.ResourcePath] = current - count;
	}
}
```

## Ресурсы `.ammo` в проекте

В `Assets/ammotype/` уже лежат готовые типы — используй их в полях `AmmoType` на оружии и пикапах:

- `9mm.ammo` — пистолетные
- `rifle.ammo` — винтовочные
- `shotgun.ammo` — дробь
- `rocket.ammo` — ракеты
- `sniper.ammo` — снайперские

## Связь с другими системами

- [06.05 — BaseWeapon.Ammo](06_05_BaseWeapon_Ammo.md): оружие получает поле `AmmoType` и проксирует `ReserveAmmo` через `AmmoInventory`. Также в `BaseWeapon.OnAdded` вызывается `AddAmmo( AmmoType, AmmoType.DefaultStartingAmmo )` для пустого пула.
- [15.02 — AmmoPickup](15_02_Pickups.md): пикап теперь ссылается на `AmmoResource`, а не на префаб оружия.
- [03.08 — PlayerInventory](03_08_PlayerInventory.md): `AmmoInventory` должен быть добавлен на префаб игрока рядом с `PlayerInventory`.

## Проверка

- [ ] Оба файла компилируются
- [ ] На префабе игрока добавлен `AmmoInventory`
- [ ] В `Assets/ammotype/` есть нужные `.ammo`-ресурсы
- [ ] Два разных оружия с одинаковым `AmmoType` делят один резерв
- [ ] `AmmoPickup` с заданным `AmmoType` корректно пополняет пул у любой владеющей пушки
- [ ] На клиенте `TakeAmmo`/`AddAmmo`/`SetAmmo` проходят через RPC на хост (`[Rpc.Host]`)
- [ ] `Pool` реплицируется с хоста на клиентов (`SyncFlags.FromHost`)

---

## ➡️ Следующий шаг

Возвращайся к [06.06 — BaseBulletWeapon](06_06_BaseBulletWeapon.md) или перейди к новому [06.11 — Projectile](06_11_Projectile.md).
