# 05.02 — Компонент: Событие физ-пушки (IPhysgunEvent) 🔫

## Что мы делаем?

Создаём **IPhysgunEvent** — интерфейс, который позволяет любому компоненту реагировать на попытку захвата объекта физ-пушкой (Physgun). Компоненты могут отклонить захват, установив `Cancelled = true`.

## Зачем это нужно?

Физ-пушка — основной инструмент перемещения объектов в Sandbox. Однако не все объекты должны быть перемещаемы: например, объекты других игроков при включённой защите пропов. `IPhysgunEvent` даёт компонентам возможность перехватить событие захвата и при необходимости отменить его. Это реализация паттерна «событие сцены» (`ISceneEvent`), который движок s&box рассылает всем компонентам на `GameObject`.

## Как это работает внутри движка?

- `ISceneEvent<IPhysgunEvent>` — базовый интерфейс движка для событий сцены. Движок автоматически находит все компоненты, реализующие интерфейс, и вызывает метод.
- `GrabEvent` — вложенный класс-контейнер данных события. Содержит `Grabber` (кто хватает) и `Cancelled` (флаг отмены).
- `OnPhysgunGrab(GrabEvent e)` — метод интерфейса с реализацией по умолчанию (пустой). Компонент реализует его только если хочет перехватить событие.

## Создай файл

Путь: `Code/Components/IPhysgunEvent.cs`

```csharp
public interface IPhysgunEvent : ISceneEvent<IPhysgunEvent>
{
	public class GrabEvent
	{
		/// <summary>
		/// The connection attempting to grab this object.
		/// </summary>
		public Connection Grabber { get; init; }

		/// <summary>
		/// Set to true to cancel the grab.
		/// </summary>
		public bool Cancelled { get; set; }
	}

	/// <summary>
	/// Called when a player attempts to grab this object with the physgun.
	/// Set <see cref="GrabEvent.Cancelled"/> to true to reject the grab.
	/// </summary>
	void OnPhysgunGrab( GrabEvent e ) { }
}
```

## Проверка

После создания файла проект должен компилироваться. Интерфейс используется компонентом `Ownable` (05.08) и будет задействован физ-пушкой в фазе оружия.
