# 06.05 — Система боеприпасов (BaseWeapon.Ammo) 🔫

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём partial-часть `BaseWeapon`, управляющую системой боеприпасов: магазин (clip), резерв (reserve), расход и пополнение патронов. Резерв может храниться **локально на оружии** (классический режим) либо **в общем пуле на игроке** через [AmmoResource и AmmoInventory](06_10_AmmoResource_Inventory.md) — тогда разные пушки с одним типом патронов делят один склад.

## Зачем это нужно?

- **Гибкая конфигурация**: оружие может не использовать патроны (`UsesAmmo = false`), использовать магазины (`UsesClips = true`) или стрелять напрямую из резерва.
- **Магазин vs резерв**: `ClipContents` — текущие патроны в обойме, `ReserveAmmo` — запас. При перезарядке патроны переливаются из резерва в магазин.
- **Общий пул патронов**: если задан `AmmoType` (`AmmoResource`) — резерв берётся/кладётся в `AmmoInventory` игрока по ключу ресурса. Два оружия с одинаковым `AmmoType` разделяют один резерв (как пистолет и SMG на 9мм).
- **Персональный резерв**: если `AmmoType` не задан — резерв хранится прямо на компоненте в поле `_reserveAmmo` и синхронизируется `[Sync]`.
- **Контроль расхода**: `TakeAmmo()` расходует патроны из магазина (или резерва/пула если без обойм), `HasAmmo()` проверяет наличие.
- **Пополнение**: `AddReserveAmmo()` добавляет патроны с ограничением `MaxReserveAmmo` (или `AmmoType.MaxReserve` если используется пул).
- **Начальный запас**: `StartingAmmo` задаёт стартовый локальный резерв. Для пула его роль выполняет `AmmoResource.DefaultStartingAmmo` — им подсеивается общий пул, когда игрок впервые берёт оружие этого типа (см. `BaseWeapon.OnAdded`).

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `UsesAmmo` | `[FeatureEnabled]` — включает/выключает всю систему патронов. Без патронов `TakeAmmo()` всегда возвращает `true`. |
| `UsesClips` | Переключает между режимом с обоймой и без. |
| `ClipMaxSize` | Максимальный размер обоймы — целевое значение при перезарядке. |
| `ClipContents` | Текущее количество патронов в обойме. По умолчанию 20. |
| `AmmoType` | `[Property]` — `AmmoResource`, общий тип патрона. Если задан — резерв идёт через общий пул игрока (`AmmoInventory`). Если `null` — классический персональный резерв. |
| `MaxReserveAmmo` | Потолок резервных патронов. При заданном `AmmoType` возвращает `AmmoType.MaxReserve`, иначе — локальное поле `_maxReserveAmmo`. |
| `ReserveAmmo` | Прокси-свойство. При заданном `AmmoType` читает/пишет в `AmmoInventory.GetAmmo/SetAmmo`. Иначе — работает с локальным `_reserveAmmo`. |
| `_reserveAmmo` | `[Sync]` приватное поле — персональный резерв (используется только когда `AmmoType == null`). |
| `StartingAmmo` | Сколько резервных патронов выдаётся при подборе (в персональном режиме). Для общих пулов аналогичную роль играет `AmmoResource.DefaultStartingAmmo` — подсев пустого пула. |
| `ReloadTime` | Время перезарядки в секундах. |
| `GetAmmoInventory()` | Приватный хелпер: `Owner?.GetComponent<AmmoInventory>()`. |
| `TakeAmmo(int)` | При `UsesClips` снимает из обоймы. Без обойм: если есть `AmmoType` — делегирует `AmmoInventory.TakeAmmo`, иначе уменьшает `_reserveAmmo`. Возвращает `false` если не хватает. |
| `HasAmmo()` | Проверяет наличие патронов в обойме или в резерве (локальном либо в пуле через прокси `ReserveAmmo`). |
| `AddReserveAmmo(int)` | При `AmmoType` вызывает `AmmoInventory.AddAmmo`, иначе прибавляет к `_reserveAmmo` с ограничением `_maxReserveAmmo`. Возвращает фактически добавленное количество. |
| `CanSwitch()` | Всегда `true` — можно переключиться на это оружие. |

> Миграция со старого API: публичный сеттер `ReserveAmmo = X` продолжает работать и корректно маршрутизируется в пул, если задан `AmmoType`. Однако прямая запись в клиентском коде не рекомендуется — используйте `AddReserveAmmo`/`TakeAmmo`, чтобы RPC-маршрутизация `AmmoInventory` соблюдалась.

## Создай файл

**Путь:** `Code/Game/Weapon/BaseWeapon/BaseWeapon.Ammo.cs`

```csharp
public partial class BaseWeapon
{
	/// <summary>
	/// Does this weapon consume ammo at all?
	/// </summary>
	[Property, FeatureEnabled( "Ammo" )] public bool UsesAmmo { get; set; } = true;

	/// <summary>
	/// Does this weapon use clips?
	/// </summary>
	[Property, Feature( "Ammo" )] public bool UsesClips { get; set; } = true;

	/// <summary>
	/// When reloading, we'll take ammo from the reserve as much as we can to fill to this amount.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( UsesClips ), true )] public int ClipMaxSize { get; set; } = 30;

	/// <summary>
	/// The default amount of bullets in a weapon's magazine on pickup.
	/// </summary>
	[Property, Feature( "Ammo" ), ShowIf( nameof( UsesClips ), true )] public int ClipContents { get; set; } = 20;

	/// <summary>
	/// The ammo resource this weapon uses for its reserve pool.
	/// When set, reserve ammo is shared with all weapons using the same resource.
	/// When null, ammo is tracked per-weapon (tied to this weapon instance).
	/// </summary>
	[Property, Feature( "Ammo" )] public AmmoResource AmmoType { get; set; }

	/// <summary>
	/// The maximum reserve ammo this weapon can hold (used only when <see cref="AmmoType"/> is null).
	/// When <see cref="AmmoType"/> is set, the resource's <see cref="AmmoResource.MaxReserve"/> is used instead.
	/// </summary>
	[Property, Feature( "Ammo" )] public int MaxReserveAmmo
	{
		get => AmmoType?.MaxReserve ?? _maxReserveAmmo;
		set => _maxReserveAmmo = value;
	}
	private int _maxReserveAmmo = 120;

	/// <summary>
	/// The current reserve ammo. When <see cref="AmmoType"/> is set this proxies to the player's
	/// <see cref="AmmoInventory"/>; otherwise it is stored directly on the weapon.
	/// </summary>
	[Property, Feature( "Ammo" )] public int ReserveAmmo
	{
		get
		{
			if ( AmmoType is not null )
				return GetAmmoInventory()?.GetAmmo( AmmoType ) ?? 0;

			return _reserveAmmo;
		}
		set
		{
			if ( AmmoType is not null )
			{
				GetAmmoInventory()?.SetAmmo( AmmoType, value );
				return;
			}

			_reserveAmmo = value;
		}
	}

	/// <summary>
	/// Backing field for per-weapon reserve ammo (used when <see cref="AmmoType"/> is null).
	/// </summary>
	[Sync] private int _reserveAmmo { get; set; } = 0;

	/// <summary>
	/// How much reserve ammo this weapon starts with on pickup.
	/// When <see cref="AmmoType"/> is set, this seeds the shared pool only if the pool is empty.
	/// </summary>
	[Property, Feature( "Ammo" )] public int StartingAmmo { get; set; } = 0;

	/// <summary>
	/// How long does it take to reload?
	/// </summary>
	[Property, Feature( "Ammo" )] public float ReloadTime { get; set; } = 2.5f;

	/// <summary>
	/// Returns the player's <see cref="AmmoInventory"/>, or null if unavailable.
	/// </summary>
	private AmmoInventory GetAmmoInventory() => Owner?.GetComponent<AmmoInventory>();

	/// <summary>
	/// Can we switch to this gun?
	/// </summary>
	public override bool CanSwitch()
	{
		return true;
	}

	/// <summary>
	/// Takes ammo from the clip, or from reserve if not using clips.
	/// </summary>
	public bool TakeAmmo( int count )
	{
		if ( !UsesAmmo ) return true;

		if ( UsesClips )
		{
			if ( ClipContents < count )
				return false;

			ClipContents -= count;
			return true;
		}

		// No clips — take directly from reserve
		if ( AmmoType is not null )
		{
			var inv = GetAmmoInventory();
			if ( inv is null ) return false;
			return inv.TakeAmmo( AmmoType, count );
		}

		if ( _reserveAmmo < count )
			return false;

		_reserveAmmo -= count;
		return true;
	}

	/// <summary>
	/// Do we have ammo?
	/// </summary>
	public bool HasAmmo()
	{
		if ( !UsesAmmo ) return true;

		if ( UsesClips )
			return ClipContents > 0;

		return ReserveAmmo > 0;
	}

	/// <summary>
	/// Adds reserve ammo to this weapon (or the shared pool), clamped to max.
	/// Returns the actual amount added.
	/// </summary>
	public int AddReserveAmmo( int count )
	{
		if ( AmmoType is not null )
		{
			var inv = GetAmmoInventory();
			return inv?.AddAmmo( AmmoType, count ) ?? 0;
		}

		var space = _maxReserveAmmo - _reserveAmmo;
		var toAdd = Math.Min( count, space );
		_reserveAmmo += toAdd;
		return toAdd;
	}
}
```

> Связанный код: в `BaseWeapon.OnAdded` (см. [06.04 — BaseWeapon](06_04_BaseWeapon.md)) при подборе оружия с `AmmoType` подсеивается общий пул на величину `AmmoType.DefaultStartingAmmo`, если пул пуст:
> ```csharp
> if ( AmmoType is not null )
> {
>     var inv = GetAmmoInventory();
>     if ( inv is not null && !inv.HasAmmo( AmmoType ) && AmmoType.DefaultStartingAmmo > 0 )
>         inv.AddAmmo( AmmoType, AmmoType.DefaultStartingAmmo );
> }
> else if ( StartingAmmo > 0 )
> {
>     _reserveAmmo = Math.Min( StartingAmmo, _maxReserveAmmo );
> }
> ```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] При `UsesAmmo = false` — `TakeAmmo()` возвращает `true`, `HasAmmo()` возвращает `true`
- [ ] При `UsesClips = true` — расход идёт из `ClipContents`
- [ ] При `UsesClips = false` и пустом `AmmoType` — расход идёт из `_reserveAmmo`
- [ ] При заданном `AmmoType` — `TakeAmmo`/`AddReserveAmmo` работают через `AmmoInventory` игрока
- [ ] `AddReserveAmmo()` не превышает `MaxReserveAmmo` (в т.ч. `AmmoType.MaxReserve` для общего пула)
- [ ] Персональный `_reserveAmmo` синхронизируется по сети (`[Sync]`)
- [ ] Общий пул корректно реплицируется через `AmmoInventory.Pool` (`[Sync( SyncFlags.FromHost )]`)


---

# 06.06 — Система перезарядки (BaseWeapon.Reloading) 🔄

## Что мы делаем?

Создаём partial-часть `BaseWeapon`, реализующую асинхронную систему перезарядки с поддержкой инкрементальной перезарядки (по одному патрону, как в дробовике) и возможностью отмены.

## Зачем это нужно?

- **Асинхронная перезарядка**: перезарядка выполняется через `async/await` с `Task.DelaySeconds`, не блокируя игровой цикл.
- **Инкрементальная перезарядка**: для дробовиков — по одному патрону за раз, с возможностью прервать в любой момент.
- **Отмена**: `CancellationTokenSource` позволяет отменить перезарядку при нажатии на атаку.
- **Сетевая синхронизация**: `BroadcastReload()` — RPC для трансляции анимации перезарядки всем клиентам.
- **Анимация**: управление параметрами анимации через `ViewModel` — `OnReloadStart()`, `OnIncrementalReload()`, `OnReloadFinish()`.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `IncrementalReloading` | Если `true` — перезарядка по 1 патрону за цикл (дробовик). Если `false` — заполняет магазин целиком за одну задержку. |
| `CanCancelReload` | Разрешает отмену перезарядки через атаку. |
| `CancellationTokenSource` | Механизм отмены async-перезарядки. При `CancelReload()` вызывается `Cancel()`. |
| `isReloading` | Приватный флаг состояния перезарядки. |
| `CanReload()` | Проверки: используются ли обоймы, не полная ли обойма, не идёт ли перезарядка, есть ли резерв. |
| `OnReloadStart()` | Точка входа: отменяет предыдущую перезарядку, создаёт новый `CancellationTokenSource`, запускает `ReloadAsync()`. |
| `ReloadAsync()` | Цикл `while`: ждёт `ReloadTime`, переносит патроны из резерва в обойму. Для инкрементальной — по 1, иначе — сколько нужно. |
| `BroadcastReload()` | `[Rpc.Broadcast]` — устанавливает параметр `b_reload` на рендерере для анимации. |
| `finally`-блоки | Двойная защита: в `OnReloadStart` (обнуление если наш токен) и в `ReloadAsync` (вызов `OnReloadFinish`). |

## Создай файл

**Путь:** `Code/Game/Weapon/BaseWeapon/BaseWeapon.Reloading.cs`

```csharp
using System.Threading;

public partial class BaseWeapon
{
	/// <summary>
	/// Should we consume 1 bullet per reload instead of filling the clip?
	/// </summary>
	[Property, Feature( "Ammo" )]
	public bool IncrementalReloading { get; set; } = false;

	/// <summary>
	/// Can we cancel reloads?
	/// </summary>
	[Property, Feature( "Ammo" )]
	public bool CanCancelReload { get; set; } = true;

	private CancellationTokenSource reloadToken;
	private bool isReloading;

	public bool CanReload()
	{
		if ( !UsesClips ) return false;
		if ( ClipContents >= ClipMaxSize ) return false;
		if ( isReloading ) return false;
		if ( ReserveAmmo <= 0 ) return false;

		return true;
	}

	public bool IsReloading() => isReloading;

	public virtual void CancelReload()
	{
		if ( reloadToken?.IsCancellationRequested == false )
		{
			reloadToken?.Cancel();
			isReloading = false;
		}
	}

	public virtual async void OnReloadStart()
	{
		if ( !CanReload() )
			return;

		CancelReload();

		var cts = new CancellationTokenSource();
		reloadToken = cts;
		isReloading = true;

		try
		{
			await ReloadAsync( cts.Token );
		}
		finally
		{
			// Only clean up our own reload
			if ( reloadToken == cts )
			{
				isReloading = false;
				reloadToken = null;
			}
			cts.Dispose();
		}
	}

	[Rpc.Broadcast]
	private void BroadcastReload()
	{
		if ( !HasOwner ) return;

		Assert.True( Owner.Controller.IsValid(), "BaseWeapon::BroadcastReload - Player Controller is invalid!" );
		Assert.True( Owner.Controller.Renderer.IsValid(), "BaseWeapon::BroadcastReload - Renderer is invalid!" );

		Owner.Controller.Renderer.Set( "b_reload", true );
	}

	protected virtual async Task ReloadAsync( CancellationToken ct )
	{
		// Capture so we can tell if a newer reload has replaced us by the time finally runs.
		var mySource = reloadToken;

		try
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );

			BroadcastReload();

			while ( ClipContents < ClipMaxSize && !ct.IsCancellationRequested )
			{
				await Task.DelaySeconds( ReloadTime, ct );

				var needed = IncrementalReloading ? 1 : (ClipMaxSize - ClipContents);
				var available = Math.Min( needed, ReserveAmmo );

				if ( available <= 0 )
					break;

				ViewModel?.RunEvent<ViewModel>( x => x.OnIncrementalReload() );

				ReserveAmmo -= available;
				ClipContents += available;
			}
		}
		finally
		{
			if ( reloadToken == mySource )
			{
				ViewModel?.RunEvent<ViewModel>( x => x.OnReloadFinish() );
			}
		}
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `CanReload()` возвращает `false` при полной обойме, пустом резерве или текущей перезарядке
- [ ] Инкрементальная перезарядка добавляет по 1 патрону за цикл
- [ ] Обычная перезарядка заполняет обойму целиком за одну задержку
- [ ] `CancelReload()` отменяет текущую перезарядку
- [ ] Анимация перезарядки транслируется через `BroadcastReload()`
- [ ] `finally`-блоки корректно очищают состояние


---

## ➡️ Следующий шаг

Переходи к **[06.06 — Стрелковое оружие (BaseBulletWeapon) 🔫](06_06_BaseBulletWeapon.md)**.
