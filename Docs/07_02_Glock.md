# 07.02 — Пистолет Glock (GlockWeapon) 🔫

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём конкретный класс оружия `GlockWeapon` — полуавтоматический пистолет Glock, наследующий `IronSightsWeapon`.

## Зачем это нужно?

- **Быстрый полуавтомат**: темп стрельбы 0.15 сек — чуть быстрее, чем Colt 1911.
- **Полуавтоматический режим**: стреляет только при нажатии, а не при удержании.
- **Единый стиль прицела**: использует тот же круговой прицел-точку, что и Colt 1911, обеспечивая визуальную однородность для класса пистолетов.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `IronSightsWeapon` | Базовый класс с логикой прицеливания через мушку. |
| `PrimaryFireRate` | `[Property]` — интервал 0.15 сек (быстрее Colt 1911). |
| `GetPrimaryFireRate()` | Возвращает `PrimaryFireRate` для базового расчёта задержки. |
| `WantsPrimaryAttack()` | `Input.Pressed("attack1")` — полуавтоматический режим. |
| `PrimaryAttack()` | Вызывает `ShootBullet()` с темпом и описанием пули. |
| `DrawCrosshair()` | Круг-точка: чёрный (r=5) + цветной (r=3). Режим смешивания `Normal`. |

## Создай файл

**Путь:** `Code/Weapons/GlockWeapon.cs`

```csharp
using Sandbox.Rendering;

public class GlockWeapon : IronSightsWeapon
{
	[Property] public float PrimaryFireRate { get; set; } = 0.15f;

	protected override float GetPrimaryFireRate() => PrimaryFireRate;

	protected override bool WantsPrimaryAttack()
	{
		return Input.Pressed( "attack1" );
	}

	public override void PrimaryAttack()
	{
		ShootBullet( PrimaryFireRate, GetBullet() );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var color = !HasAmmo() || IsReloading() || TimeUntilNextShotAllowed > 0 ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Normal );
		hud.DrawCircle( center, 5, Color.Black );
		hud.DrawCircle( center, 3, color );
	}
}
```

## Проверка

1. Возьмите Glock и убедитесь, что каждое нажатие ЛКМ производит один выстрел.
2. Проверьте, что темп стрельбы выше, чем у Colt 1911 (0.15 vs 0.2 сек).
3. Убедитесь, что удержание ЛКМ не вызывает автоматическую стрельбу.
4. Проверьте отображение прицела: зелёная точка при готовности, красная — при невозможности стрельбы.


---

## ➡️ Следующий шаг

Переходи к **[07.03 — Штурмовая винтовка M4A1 (M4a1Weapon) 🔫](07_03_M4A1.md)**.
