# 06.08 — Оружие с механическим прицелом (IronSightsWeapon) 🎯

## Что мы делаем?

Создаём абстрактный класс `IronSightsWeapon` — расширение `BaseBulletWeapon` для оружия с прицеливанием (ADS — Aim Down Sights). Правая кнопка мыши переключает режим прицеливания вместо дополнительной атаки.

## Зачем это нужно?

- **Прицеливание (ADS)**: при зажатии ПКМ оружие переходит в режим прицеливания — скрывается перекрестье, уменьшается разброс и отдача.
- **Масштабирование параметров**: `IronSightsFireScale` уменьшает `AimConeBase` и `AimConeSpread` при прицеливании.
- **Блокировка альт-огня**: `CanSecondaryAttack()` возвращает `false` — правая кнопка используется только для ADS.
- **Анимация**: параметры `ironsights` и `ironsights_fire_scale` управляют анимацией вью-модели.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `IronSightsFireScale` | Множитель (по умолчанию 0.2) — при ADS разброс и спред умножаются на это значение. |
| `_isAiming` | Приватное поле состояния прицеливания. |
| `IsAiming` | Публичное свойство для чтения. |
| `CanSecondaryAttack()` | Всегда `false` — вторичная атака заблокирована. |
| `DrawHud()` | Скрывает HUD (перекрестье) при прицеливании. |
| `OnControl()` | Вызывает `base.OnControl()`, затем проверяет `Input.Down("attack2")` для переключения ADS. |
| `Renderer.Set("ironsights", ...)` | Устанавливает анимационный параметр для вью-модели (1 = прицеливание, 0 = обычный режим). |
| `OnDisabled()` | Сбрасывает `_isAiming` при деактивации. |
| `GetBullet()` | Возвращает `BulletConfiguration` с уменьшенным разбросом при прицеливании. |

## Создай файл

**Путь:** `Code/Game/Weapon/IronSightsWeapon.cs`

```csharp
using Sandbox.Rendering;

/// <summary>
/// A weapon that can aim down sights
/// </summary>
public abstract class IronSightsWeapon : BaseBulletWeapon
{
	/// <summary>
	/// Lowers the amount of recoil / visual noise when aiming
	/// </summary>
	[Property] public float IronSightsFireScale { get; set; } = 0.2f;

	private bool _isAiming;

	public bool IsAiming => _isAiming;

	public override bool CanSecondaryAttack() => false;

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( _isAiming ) return;
		base.DrawHud( painter, crosshair );
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		var wantsAim = Input.Down( "attack2" );

		if ( wantsAim == _isAiming )
			return;

		_isAiming = wantsAim;
		ViewModel?.RunEvent<ViewModel>( x =>
		{
			x.Renderer?.Set( "ironsights", _isAiming ? 1 : 0 );
			x.Renderer?.Set( "ironsights_fire_scale", _isAiming ? IronSightsFireScale : 1f );
		} );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		_isAiming = false;
	}

	protected BulletConfiguration GetBullet()
	{
		if ( !_isAiming )
			return Bullet;

		var config = Bullet;
		config.AimConeBase *= IronSightsFireScale;
		config.AimConeSpread *= IronSightsFireScale;
		return config;
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] При зажатии ПКМ — `_isAiming = true`, анимация переходит в режим прицеливания
- [ ] При отпускании ПКМ — `_isAiming = false`, анимация возвращается
- [ ] Перекрестье скрыто при прицеливании
- [ ] `GetBullet()` уменьшает разброс на `IronSightsFireScale`
- [ ] `CanSecondaryAttack()` всегда `false`
- [ ] При деактивации `_isAiming` сбрасывается
