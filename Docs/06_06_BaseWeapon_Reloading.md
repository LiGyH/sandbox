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
