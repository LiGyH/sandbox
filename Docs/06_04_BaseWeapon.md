# 06.04 — Базовое оружие (BaseWeapon) ⚔️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём класс `BaseWeapon` — основу для любого оружия в игре. Он наследует `BaseCarryable` и добавляет логику стрельбы, перезарядки, прицел и поддержку управления через сиденья (`IPlayerControllable`).

## Зачем это нужно?

- **Время развёртывания**: после переключения оружия есть задержка (`DeployTime`), в течение которой нельзя стрелять.
- **Контроль скорострельности**: `TimeUntilNextShotAllowed` и `AddShootDelay()` управляют интервалами между выстрелами.
- **Сухой выстрел / авто-перезарядка**: `DryFire()` и `TryAutoReload()` — две стратегии для пустого магазина.
- **Цикл управления**: `OnControl()` обрабатывает ввод — перезарядка, отмена перезарядки, основная и дополнительная атаки.
- **Прицел (Crosshair)**: `DrawCrosshair()` рисует стандартный красный перекрестье через `HudPainter`.
- **Управление через сиденья**: `IPlayerControllable` + `ShootInput` / `SecondaryInput` позволяют использовать оружие из транспорта.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `DeployTime` | Задержка после активации оружия, блокирующая стрельбу. |
| `ShouldAvoid` | Переопределяет базовый — возвращает `true`, если нет патронов (для авто-переключения). |
| `TimeUntilNextShotAllowed` | Таймер `TimeUntil` — обратный отсчёт до разрешения следующего выстрела. |
| `DryFire()` | Воспроизводит звук сухого щелчка, если нет патронов и не идёт перезарядка. |
| `TryAutoReload()` | Вызывает `DryFire()`, затем пытается начать перезарядку `OnReloadStart()`. |
| `OnPlayerUpdate()` | Управляет вью-моделью + вызывает `OnControl()` только для локального игрока. |
| `OnControl()` | Основной цикл ввода: отмена перезарядки → ручная перезарядка → атака1 → атака2. **Сразу выходит, если у оружия есть владелец (`HasOwner`)** — иначе при стрельбе из рук ввод срабатывал бы дважды (через `OnPlayerUpdate` и через путь `IPlayerControllable`). Этот код-путь используется только в seat / standalone-режиме. |
| `CanPrimaryAttack() / CanSecondaryAttack()` | Проверки: есть ли патроны, не идёт ли перезарядка, прошёл ли таймер. |
| `IPlayerControllable` | Интерфейс для управления из транспортных сидений с `ClientInput`. |
| `DrawCrosshair()` | Рисует 4 линии перекрестья через `HudPainter.DrawLine()`. |

## Создай файл

**Путь:** `Code/Game/Weapon/BaseWeapon/BaseWeapon.cs`

```csharp
using Sandbox.Rendering;

public partial class BaseWeapon : BaseCarryable, IPlayerControllable
{
	/// <summary>
	/// How long after deploying a weapon can you not shoot a gun?
	/// </summary>
	[Property] public float DeployTime { get; set; } = 0.5f;

	public override bool ShouldAvoid => !HasAmmo();

	/// <summary>
	/// How long until we can shoot again
	/// </summary>
	protected TimeUntil TimeUntilNextShotAllowed;

	/// <summary>
	/// Adds a delay, making it so we can't shoot for the specified time
	/// </summary>
	/// <param name="seconds"></param>
	public void AddShootDelay( float seconds )
	{
		TimeUntilNextShotAllowed = seconds;
	}

	/// <summary>
	/// The dry fire sound if we have no ammo
	/// </summary>
	private static SoundEvent DryFireSound = new SoundEvent( "sounds/dry_fire.sound" );

	/// <summary>
	/// Play a dry fire sound. You should only call this on weapons that can't auto reload - if they can, use <see cref="TryAutoReload"/> instead.
	/// </summary>
	public void DryFire()
	{
		if ( HasAmmo() )
			return;

		if ( IsReloading() )
			return;

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		GameObject.PlaySound( DryFireSound );
	}

	/// <summary>
	/// Player has fired an empty gun - play dry fire sound and start reloading. You should only call this on weapons that can reload - if they can't, use <see cref="DryFire"/> instead.
	/// </summary>
	public virtual void TryAutoReload()
	{
		if ( HasAmmo() )
			return;

		if ( IsReloading() )
			return;

		if ( TimeUntilNextShotAllowed > 0 )
			return;

		DryFire();

		AddShootDelay( 0.1f );

		if ( CanReload() )
			OnReloadStart();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		AddShootDelay( DeployTime );
	}

	public override void OnAdded( Player player )
	{
		base.OnAdded( player );

		if ( UsesAmmo && StartingAmmo > 0 )
		{
			ReserveAmmo = Math.Min( StartingAmmo, MaxReserveAmmo );
		}
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		DrawCrosshair( painter, crosshair );
	}

	public override void OnPlayerUpdate( Player player )
	{
		if ( player is null ) return;

		if ( !player.Controller.ThirdPerson )
		{
			CreateViewModel();
		}
		else
		{
			DestroyViewModel();
		}

		GameObject.Network.Interpolation = false;

		if ( !player.IsLocalPlayer )
			return;

		OnControl( player );
	}

	public override void OnControl( Player player )
	{
		bool wantsToCancelReload = Input.Pressed( "Attack1" ) || Input.Pressed( "Attack2" );
		if ( CanCancelReload && IsReloading() && wantsToCancelReload && HasAmmo() )
		{
			CancelReload();
		}

		if ( CanReload() && Input.Pressed( "reload" ) )
		{
			OnReloadStart();
		}

		if ( CanPrimaryAttack() && WantsPrimaryAttack() )
		{
			PrimaryAttack();
		}

		if ( CanSecondaryAttack() && WantsSecondaryAttack() )
		{
			SecondaryAttack();
		}
	}

	protected virtual bool WantsSecondaryAttack()
	{
		return Input.Down( "attack2" );
	}

	protected virtual bool WantsPrimaryAttack()
	{
		return Input.Down( "attack1" );
	}

	/// <summary>
	/// Override to perform the weapon's primary attack. Default no-op.
	/// </summary>
	public virtual void PrimaryAttack()
	{
	}

	/// <summary>
	/// Override to perform the weapon's secondary attack. Default no-op.
	/// </summary>
	public virtual void SecondaryAttack()
	{
	}

	/// <summary>
	/// Determines if the primary attack should trigger
	/// </summary>
	public virtual bool CanPrimaryAttack()
	{
		if ( HasOwner && !HasAmmo() ) return false;
		if ( IsReloading() ) return false;
		if ( TimeUntilNextShotAllowed > 0 ) return false;

		return true;
	}

	/// <summary>
	/// Determines if the secondary attack should trigger
	/// </summary>
	public virtual bool CanSecondaryAttack()
	{
		if ( HasOwner && !HasAmmo() ) return false;
		if ( IsReloading() ) return false;
		if ( TimeUntilNextShotAllowed > 0 ) return false;

		return true;
	}

	/// <summary>
	/// Override the primary fire rate
	/// </summary>
	protected virtual float GetPrimaryFireRate() => 0.1f;

	/// <summary>
	/// Override the secondary fire rate
	/// </summary>
	protected virtual float GetSecondaryFireRate() => 0.2f;

	/// <summary>
	/// The input that fires the primary attack when this weapon is controlled via a seat.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput ShootInput { get; set; }

	/// <summary>
	/// The input that fires the secondary attack when this weapon is controlled via a seat.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Inputs" )] public ClientInput SecondaryInput { get; set; }

	/// <summary>
	/// Сидя в стуле, оружие может управлять только тот, у кого нет своего активного оружия.
	/// Это нужно, чтобы игрок с РПГ в руках не «перехватывал» наводку у пушки контрапции.
	/// См. <see cref="IPlayerControllable.CanControl(Player)"/>.
	/// </summary>
	public bool CanControl( Player player )
	{
		var inventory = player.GetComponent<PlayerInventory>();
		return inventory is null || !inventory.ActiveWeapon.IsValid();
	}

	public void OnStartControl() { }

	public void OnEndControl() { }

	public virtual void OnControl()
	{
		if ( HasOwner ) return;
		// Standalone-оружие в сцене всё ещё получает OnControl на не-хост клиентах
		// (через ControlSystem), но физическое исполнение должно идти строго на хосте.
		if ( IsProxy ) return;

		if ( ShootInput.Down() && CanPrimaryAttack() )
			PrimaryAttack();

		if ( SecondaryInput.Down() && CanSecondaryAttack() )
			SecondaryAttack();
	}

	public virtual void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = Color.Red;

		hud.DrawLine( center + Vector2.Left * 32, center + Vector2.Left * 15, 3, color );
		hud.DrawLine( center - Vector2.Left * 32, center - Vector2.Left * 15, 3, color );
		hud.DrawLine( center + Vector2.Up * 32, center + Vector2.Up * 15, 3, color );
		hud.DrawLine( center - Vector2.Up * 32, center - Vector2.Up * 15, 3, color );
	}
	protected Color CrosshairCanShoot => Color.White;
	protected Color CrosshairNoShoot => Color.Red;
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] При активации оружия устанавливается задержка `DeployTime`
- [ ] `OnControl()` правильно обрабатывает последовательность: отмена перезарядки → перезарядка → атака1 → атака2
- [ ] `DryFire()` воспроизводит звук только при отсутствии патронов
- [ ] `TryAutoReload()` начинает перезарядку после сухого щелчка
- [ ] `DrawCrosshair()` рисует 4 линии перекрестья
- [ ] `IPlayerControllable` позволяет управлять через сиденья


---

## ➡️ Следующий шаг

Переходи к **[06.05 — Система боеприпасов (BaseWeapon.Ammo) 🔫](06_05_BaseWeapon_Ammo.md)**.
