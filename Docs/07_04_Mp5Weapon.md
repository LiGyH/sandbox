# 07.04 — Пистолет-пулемёт MP5 (Mp5Weapon) 🔫

## Что мы делаем?

Создаём конкретный класс оружия `Mp5Weapon` — автоматический пистолет-пулемёт MP5, наследующий `IronSightsWeapon`.

## Зачем это нужно?

- **Автоматический огонь**: стреляет при удержании ЛКМ, как и M4A1.
- **Свой класс оружия**: хотя код похож на M4A1, отдельный класс позволяет задать уникальные параметры пули (урон, разброс, отдача) через инспектор.
- **Динамический крестообразный прицел**: реагирует на текущий разброс, визуально показывая точность.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `IronSightsWeapon` | Базовый класс с автоматическим огнём по умолчанию. |
| `TimeBetweenShots` | `[Property]` — 0.1 сек между выстрелами. |
| `GetPrimaryFireRate()` | Возвращает `TimeBetweenShots`. |
| `PrimaryAttack()` | Стреляет через `ShootBullet()`. |
| `DrawCrosshair()` | 4 линии (крест) с `Lighten`. Зазор динамически зависит от `GetAimConeAmount()`. |

## Создай файл

**Путь:** `Code/Weapons/Mp5/Mp5Weapon.cs`

```csharp
using Sandbox.Rendering;

public class Mp5Weapon : IronSightsWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 0.1f;

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override void PrimaryAttack()
	{
		ShootBullet( TimeBetweenShots, GetBullet() );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var gap = 16 + GetAimConeAmount() * 32;
		var len = 12;
		var w = 2f;

		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
		hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
		hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
		hud.DrawLine( center - Vector2.Up * (len + gap), center - Vector2.Up * gap, w, color );
	}
}
```

## Проверка

1. Возьмите MP5 и убедитесь, что удержание ЛКМ вызывает автоматическую стрельбу.
2. Проверьте, что крестообразный прицел раздвигается при стрельбе.
3. Сравните с M4A1 — оба должны стрелять с одинаковым темпом (0.1 сек), но могут отличаться уроном и разбросом (настраивается через инспектор).
4. Убедитесь, что прицел меняет цвет при пустом магазине.
