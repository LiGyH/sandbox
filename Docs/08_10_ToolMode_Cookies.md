# 🍪 ToolMode.Cookies — Сохранение настроек инструмента

## Что мы делаем?

Создаём файл `ToolMode.Cookies.cs` — partial-часть класса `ToolMode`, определяющая префикс для системы сохранения настроек (cookies) каждого инструмента.

## Зачем это нужно?

Каждый инструмент может иметь пользовательские настройки (материал, цвет, размер и т.д.). Система cookies автоматически сохраняет и восстанавливает эти настройки между сессиями. `CookiePrefix` обеспечивает уникальный ключ для каждого типа инструмента.

## Как это работает внутри движка?

- `ToolMode` реализует интерфейс `ICookieSource`
- `CookiePrefix` — виртуальное свойство, возвращающее строку вида `tool.имякласса` (в нижнем регистре)
- Используется методами `LoadCookies()` и `SaveCookies()` из `OnEnabled`/`OnDisabled`
- Конкретные инструменты могут переопределить `CookiePrefix` для кастомного пространства имён

## Создай файл

📄 `Code/Weapons/ToolGun/ToolMode.Cookies.cs`

```csharp
﻿partial class ToolMode : ICookieSource
{
	public virtual string CookiePrefix => $"tool.{GetType().Name.ToLowerInvariant()}";
}
```

## Проверка

- Файл `Code/Weapons/ToolGun/ToolMode.Cookies.cs` создан как partial-класс `ToolMode : ICookieSource`
- `CookiePrefix` генерирует уникальный ключ на основе имени типа
