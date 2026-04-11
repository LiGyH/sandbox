# 🔒 IToolgunEvent.cs — Интерфейс событий тулгана

## Что мы делаем?

Создаём файл `IToolgunEvent.cs` — интерфейс, позволяющий объектам **блокировать** или **разрешать** взаимодействие с тулганом.

## Зачем это нужно?

В многопользовательской игре игрок **не должен** использовать инструменты на чужих объектах (если это запрещено настройками сервера). Компонент `Ownable` (из фазы 5) реализует `IToolgunEvent` и может отклонить попытку:

```
Игрок пытается использовать Weld на чужом объекте
  └── TraceSelect() (в ToolMode.Helpers.cs)
        └── RunEvent<IToolgunEvent>( OnToolgunSelect )
              └── Ownable проверяет владельца
                    ├── Свой объект → разрешено
                    └── Чужой → selectEvent.Cancelled = true → TraceSelect вернёт default
```

## Как это работает внутри движка?

### ISceneEvent<T>

`IToolgunEvent` наследует `ISceneEvent<IToolgunEvent>` — это механизм движка s&box для рассылки событий по иерархии `GameObject`:

```csharp
sp.GameObject.Root.RunEvent<IToolgunEvent>( x => x.OnToolgunSelect( selectEvent ) );
```

- `.Root` — начинаем с корня объекта
- `RunEvent<IToolgunEvent>` — вызывает `OnToolgunSelect` на **всех** компонентах, реализующих `IToolgunEvent`, во всей иерархии

### SelectEvent

Вложенный класс `SelectEvent` содержит:
- `User` — `Connection` игрока, который пытается использовать инструмент
- `Cancelled` — установить в `true`, чтобы заблокировать

### Метод по умолчанию

```csharp
void OnToolgunSelect( SelectEvent e ) { }
```

Пустая реализация по умолчанию — если компонент не переопределяет метод, взаимодействие **разрешено**.

## Ссылки на движок

- [`ISceneEvent<T>`](https://github.com/LiGyH/sbox-public) — базовый интерфейс для событий в сцене
- [`Connection`](https://github.com/LiGyH/sbox-public) — представляет подключённого клиента
- `RunEvent<T>()` — вызов события на всех компонентах в иерархии

## Создай файл

📄 `Code/Components/IToolgunEvent.cs`

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

## Аналогия с IPhysgunEvent

Этот интерфейс работает точно так же, как `IPhysgunEvent` (из фазы 8). Разница только в том, **когда** он вызывается:
- `IPhysgunEvent` — при попытке захватить объект физганом
- `IToolgunEvent` — при попытке навести тулган на объект

Компонент `Ownable` реализует **оба** интерфейса, обеспечивая единую систему владения объектами.

## Что проверить?

Файл компилируется самостоятельно. Убедитесь, что `Ownable.cs` (фаза 5) реализует `IToolgunEvent` — если нет, его нужно обновить.

---

➡️ Следующий шаг: [09_09 — ToolInfoPanel (UI)](09_09_ToolInfoPanel.md)
