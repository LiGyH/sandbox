# 06.05 — Система боеприпасов (BaseWeapon.Ammo) 🔫

## Что мы делаем?

Создаём partial-часть `BaseWeapon`, управляющую системой боеприпасов: магазин (clip), резерв (reserve), расход и пополнение патронов.

## Зачем это нужно?

- **Гибкая конфигурация**: оружие может не использовать патроны (`UsesAmmo = false`), использовать магазины (`UsesClips = true`) или стрелять напрямую из резерва.
- **Магазин vs резерв**: `ClipContents` — текущие патроны в обойме, `ReserveAmmo` — запас. При перезарядке патроны переливаются из резерва в магазин.
- **Контроль расхода**: `TakeAmmo()` расходует патроны из магазина (или резерва если без обойм), `HasAmmo()` проверяет наличие.
- **Пополнение**: `AddReserveAmmo()` добавляет патроны с ограничением `MaxReserveAmmo`.
- **Начальный запас**: `StartingAmmo` задаёт стартовый резерв при подборе оружия.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `UsesAmmo` | `[FeatureEnabled]` — включает/выключает всю систему патронов. Без патронов `TakeAmmo()` всегда возвращает `true`. |
| `UsesClips` | Переключает между режимом с обоймой и без. |
| `ClipMaxSize` | Максимальный размер обоймы — целевое значение при перезарядке. |
| `ClipContents` | Текущее количество патронов в обойме. По умолчанию 20. |
| `MaxReserveAmmo` | Потолок резервных патронов. |
| `ReserveAmmo` | `[Sync]` — синхронизированный текущий резерв. |
| `StartingAmmo` | Сколько резервных патронов выдаётся при подборе. |
| `ReloadTime` | Время перезарядки в секундах. |
| `TakeAmmo(int)` | Отнимает патроны: из обоймы (при `UsesClips`) или из резерва. Возвращает `false` если не хватает. |
| `HasAmmo()` | Проверяет наличие патронов в обойме или резерве. |
| `AddReserveAmmo(int)` | Добавляет резерв, ограничивая максимумом. Возвращает фактически добавленное количество. |
| `CanSwitch()` | Всегда `true` — можно переключиться на это оружие. |

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
	/// The maximum reserve ammo this weapon can hold.
	/// </summary>
	[Property, Feature( "Ammo" )] public int MaxReserveAmmo { get; set; } = 120;

	/// <summary>
	/// The current reserve ammo on this weapon.
	/// </summary>
	[Sync, Property, Feature( "Ammo" )] public int ReserveAmmo { get; set; } = 0;

	/// <summary>
	/// How much reserve ammo this weapon starts with on pickup.
	/// </summary>
	[Property, Feature( "Ammo" )] public int StartingAmmo { get; set; } = 0;

	/// <summary>
	/// How long does it take to reload?
	/// </summary>
	[Property, Feature( "Ammo" )] public float ReloadTime { get; set; } = 2.5f;
	
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
		if ( ReserveAmmo < count )
			return false;

		ReserveAmmo -= count;
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
	/// Adds reserve ammo to this weapon, clamped to max.
	/// </summary>
	public int AddReserveAmmo( int count )
	{
		var space = MaxReserveAmmo - ReserveAmmo;
		var toAdd = Math.Min( count, space );
		ReserveAmmo += toAdd;
		return toAdd;
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] При `UsesAmmo = false` — `TakeAmmo()` возвращает `true`, `HasAmmo()` возвращает `true`
- [ ] При `UsesClips = true` — расход идёт из `ClipContents`
- [ ] При `UsesClips = false` — расход идёт из `ReserveAmmo`
- [ ] `AddReserveAmmo()` не превышает `MaxReserveAmmo`
- [ ] `ReserveAmmo` синхронизируется по сети (`[Sync]`)


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
