# 11.04 — Базовый класс страницы утилит (UtilityPage) 🧰

## Что мы делаем?

Создаём **UtilityPage** — абстрактный базовый класс для страниц, отображаемых в правой панели вкладки «Utilities» спавн-меню.

## Как это работает?

`UtilityPage` наследует `Panel` (UI-элемент движка). Каждая конкретная страница утилит (настройки сервера, пост-обработка и т.д.) наследует `UtilityPage` и декорируется атрибутами `[Icon]`, `[Title]`, `[Group]`, `[Order]` для отображения в меню.

Метод `IsVisible()` позволяет скрывать страницу в зависимости от условий (например, скрыть серверные настройки для не-хоста).

## Создай файл

Путь: `Code/Game/UtilityFunctions/UtilityPage.cs`

```csharp
using Sandbox.UI;

/// <summary>
/// Base class for pages opened in the utility tab right panel.
/// Decorate subclasses with [Icon], [Title], [Group], and [Order].
/// </summary>
public abstract class UtilityPage : Panel
{
	/// <summary>
	/// Return false to hide this page from the utility menu.
	/// </summary>
	public virtual bool IsVisible() => true;
}
```

## Как используется

UI-компонент `UtilitiesPage.razor` собирает все наследники `UtilityPage` через `TypeLibrary`, фильтрует по `IsVisible()`, сортирует по `[Order]` и отображает как список.

## Результат

После создания этого файла:
- Готова база для добавления страниц утилит
- Каждая новая страница — отдельный класс-наследник

---

Следующий шаг: [12.01 — Спавнер игроков на карте (MapPlayerSpawner)](12_01_MapPlayerSpawner.md)
