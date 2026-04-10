# 📺 Toolgun.Screen — Экран инструментальной пушки

## Что мы делаем?

Создаём файл `Toolgun.Screen.cs` — partial-часть класса `Toolgun`, делегирующая отрисовку содержимого экрана на viewmodel текущему активному режиму инструмента.

## Зачем это нужно?

Каждый инструмент может иметь свой уникальный экран — название, иконку, параметры. Toolgun лишь перенаправляет вызов отрисовки в текущий `ToolMode`, обеспечивая полиморфизм отображения.

## Как это работает внутри движка?

- `DrawScreenContent` переопределяет метод базового класса `ScreenWeapon`
- Получает текущий активный режим через `GetCurrentMode()`
- Вызывает `currentMode?.DrawScreen(rect, paint)` — каждый ToolMode рисует свой экран

## Создай файл

📄 `Code/Weapons/ToolGun/Toolgun.Screen.cs`

```csharp
﻿using Sandbox.Rendering;

public partial class Toolgun : ScreenWeapon
{
	protected override void DrawScreenContent( Rect rect, HudPainter paint )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawScreen( rect, paint );
	}
}
```

## Проверка

- Файл `Code/Weapons/ToolGun/Toolgun.Screen.cs` создан как partial-класс `Toolgun : ScreenWeapon`
- Метод `DrawScreenContent` делегирует отрисовку текущему `ToolMode`
