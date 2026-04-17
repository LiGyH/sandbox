# 09.08 — Компонент: Событие тул-гана (IToolgunEvent) 🔧

## Что мы делаем?

Создаём **IToolgunEvent** — интерфейс, позволяющий компонентам реагировать на попытку использования тул-гана (Toolgun) на объекте. Компоненты могут отклонить выбор, установив `Cancelled = true`.

## Зачем это нужно?

Тул-ган — инструмент, через который игроки применяют инструменты (Weld, Rope, Color и т.д.) к объектам. Аналогично `IPhysgunEvent`, не все объекты должны быть доступны для модификации. Например, при включённой защите пропов (`sb.ownership_checks`) объекты других игроков должны быть защищены. `IToolgunEvent` позволяет компонентам перехватить выбор объекта и при необходимости отменить его.

## Как это работает внутри движка?

- `ISceneEvent<IToolgunEvent>` — базовый интерфейс движка для событий сцены. Движок рассылает событие всем компонентам на `GameObject`, реализующим этот интерфейс.
- `SelectEvent` — вложенный класс с данными события. Содержит `User` (кто использует тул-ган) и `Cancelled` (флаг отмены).
- `OnToolgunSelect(SelectEvent e)` — метод интерфейса с пустой реализацией по умолчанию. Компонент переопределяет его при необходимости.

## Создай файл

Путь: `Code/Components/IToolgunEvent.cs`

```csharp
public interface IToolgunEvent : ISceneEvent<IToolgunEvent>
{
	public class SelectEvent
	{
		/// <summary>
		/// The connection attempting to use a tool on this object.
		/// </summary>
		public Connection User { get; init; }

		/// <summary>
		/// Set to true to reject the toolgun selection.
		/// </summary>
		public bool Cancelled { get; set; }
	}

	/// <summary>
	/// Called when a player attempts to select this object with the toolgun.
	/// Set <see cref="SelectEvent.Cancelled"/> to true to reject the selection.
	/// </summary>
	void OnToolgunSelect( SelectEvent e ) { }
}
```

## Проверка

После создания файла проект должен компилироваться. Интерфейс используется компонентом `Ownable` (05.08) и будет задействован тул-ганом в фазе оружия.


---

## ➡️ Следующий шаг

Переходи к **[09.09 — Этап 09_09 — ToolInfoPanel (Панель информации об инструменте)](09_09_ToolInfoPanel.md)**.
