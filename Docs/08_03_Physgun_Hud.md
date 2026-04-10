# 🎯 Physgun.Hud — HUD-прицел физической пушки

## Что мы делаем?

Создаём файл `Physgun.Hud.cs` — partial-часть класса `Physgun`, отвечающая за отрисовку HUD-элементов прицела на экране игрока.

## Зачем это нужно?

Прицел визуально информирует игрока о текущем состоянии физической пушки:
- Маленький яркий кружок, когда объект под прицелом можно захватить
- Большой полупрозрачный кружок в режиме ожидания
- Прицел скрывается, когда объект уже захвачен (чтобы не мешать)

## Как это работает внутри движка?

- `DrawHud` переопределяет метод базового класса `ScreenWeapon`
- Если объект захвачен (`_state.IsValid()`) — ничего не рисуется
- Если есть наведённый объект (`_stateHovered.IsValid()`) — рисуется маленький яркий кружок (радиус 3, цвет Cyan)
- Иначе — рисуется большой полупрозрачный кружок (радиус 5, Cyan с alpha 0.2)

## Создай файл

📄 `Code/Weapons/PhysGun/Physgun.Hud.cs`

```csharp
﻿using Sandbox.Rendering;

public partial class Physgun : ScreenWeapon
{
	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( _state.IsValid() )
			return;

		if ( _stateHovered.IsValid() )
		{
			painter.DrawCircle( crosshair, 3, Color.Cyan );
		}
		else
		{
			painter.DrawCircle( crosshair, 5, Color.Cyan.WithAlpha( 0.2f ) );
		}


	}

}
```

## Проверка

- Файл `Code/Weapons/PhysGun/Physgun.Hud.cs` создан как partial-класс `Physgun : ScreenWeapon`
- Метод `DrawHud` корректно обрабатывает три состояния: захвачен, наведён, пусто
- Используется `HudPainter.DrawCircle` для отрисовки прицела
