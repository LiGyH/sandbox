# 17.01 — Система управления транспортом (ControlSystem) 🚗

## Что мы делаем?

Создаём **ControlSystem** и вспомогательную структуру **ClientInput** — систему, которая позволяет игрокам, сидящим в «стульях» (BaseChair), управлять связанными с ними объектами (машины, катапульты, любые контрапции).

## Как это работает?

Когда игрок садится в стул (`BaseChair`), `ControlSystem` собирает все физически связанные объекты через `LinkedGameObjectBuilder` (тот же, что в Duplicator). Затем для каждого связанного объекта вызывает `IPlayerControllable.OnControl()`, передавая ввод игрока.

### Поток работы

1. Каждый фиксированный тик — собираем все занятые стулья
2. Сортируем по времени посадки (кто сел первым — управляет первым)
3. Для каждого стула — строим граф связанных объектов
4. Если граф уже обработан другим стулом — пропускаем
5. Подменяем глобальный ввод на ввод конкретного игрока (`ClientInput.PushScope`)
6. Вызываем `OnControl()` на всех `IPlayerControllable` в графе

## Создай файл: ClientInput

Путь: `Code/Game/ControlSystem/ClientInput.cs`

```csharp
using Sandbox.Utility;

public struct ClientInput
{
	readonly record struct State( Connection connection, PlayerController playerController );

	static State _currentState;

	static Connection Connection => _currentState.connection;

	public readonly bool IsEnabled => !string.IsNullOrWhiteSpace( Action );

	public string Action { get; set; }

	/// <summary>
	/// Returns an analog value between 0 and 1 representing how much the input is pressed
	/// </summary>
	public readonly float GetAnalog()
	{
		if ( !IsEnabled ) return 0;
		return Down() ? 1 : 0;
	}

	/// <summary>
	/// Returns true if button is currently held down
	/// </summary>
	public readonly bool Down()
	{
		if ( !IsEnabled ) return false;

		return Connection?.Down( Action ) ?? false;
	}

	/// <summary>
	/// Returns true if button was released
	/// </summary>
	public readonly bool Released()
	{
		if ( !IsEnabled ) return false;

		return Connection?.Released( Action ) ?? false;
	}

	/// <summary>
	/// Returns true if button was pressed
	/// </summary>
	public readonly bool Pressed()
	{
		if ( !IsEnabled ) return false;

		return Connection?.Pressed( Action ) ?? false;
	}

	internal static IDisposable PushScope( PlayerController player )
	{
		var previousState = _currentState;
		_currentState = new State( player?.Network?.Owner, player );

		return DisposeAction.Create( () => _currentState = previousState );
	}
}
```

### Зачем PushScope?

`ClientInput.PushScope` — это паттерн **scope-based override**. Он временно подменяет «текущего игрока» для всех объектов, вызывающих `ClientInput.Down()` и т.д. После выхода из `using` — восстанавливается предыдущее состояние. Это позволяет контрапциям использовать единый API ввода, не зная, кто именно ими управляет.

## Создай файл: ControlSystem

Путь: `Code/Game/ControlSystem/ControlSystem.cs`

```csharp
public class ControlSystem : GameObjectSystem<ControlSystem>
{
	// When each chair first became occupied. Used to sort seats so the earliest occupant is in charge
	private readonly Dictionary<BaseChair, RealTimeSince> _occupiedSince = new();

	public ControlSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 10, OnTick, "ControlSystem" );
	}

	void OnTick()
	{
		// TODO this should be more generic, some kind of interface?
		var driven = new HashSet<GameObject>();

		foreach ( var chair in GetSortedSeats() )
		{
			var builder = new LinkedGameObjectBuilder();
			builder.AddConnected( chair.GameObject );

			// Skip if a seat occupied earlier already claimed this
			if ( builder.Objects.Any( driven.Contains ) ) continue;
			driven.UnionWith( builder.Objects );

			RunControl( chair, builder );
		}
	}

	IEnumerable<BaseChair> GetSortedSeats()
	{
		foreach ( var chair in Scene.GetAll<BaseChair>() )
		{
			if ( !chair.IsValid() || !chair.IsOccupied )
				_occupiedSince.Remove( chair );
			else
				_occupiedSince.TryAdd( chair, 0 );
		}

		return Scene.GetAll<BaseChair>()
			.Where( c => c.IsValid() && c.IsOccupied )
			.OrderBy( c => (float)_occupiedSince.GetValueOrDefault( c, default ) );
	}

	void RunControl( BaseChair chair, LinkedGameObjectBuilder builder )
	{
		var player = chair.GetOccupant();
		if ( !player.IsValid() ) return;

		using var scope = ClientInput.PushScope( player );

		foreach ( var o in builder.Objects )
		{
			foreach ( var controllable in o.GetComponentsInChildren<IPlayerControllable>() )
			{
				controllable?.OnControl();
			}
		}
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `Listen(Stage.StartFixedUpdate, 10, OnTick)` | Подписка на тик физики с приоритетом 10 |
| `LinkedGameObjectBuilder` | Строит граф физически связанных объектов (constraints, joints) |
| `driven` | Предотвращает двойную обработку одной контрапции |
| `PushScope` | Подменяет «текущего игрока» для ввода |
| `IPlayerControllable.OnControl()` | Вызывается для каждого управляемого компонента |

## Связь с другими системами

- **IPlayerControllable** (см. [02.02](02_02_IPlayerControllable.md)) — интерфейс, который реализуют управляемые объекты
- **LinkedGameObjectBuilder** (из Duplicator) — строит граф связей
- **BaseChair** — стул из движка, в который садится игрок

## Результат

После создания этих файлов:
- Игрок может сесть в стул и управлять всей связанной контрапцией
- Несколько стульев на одной контрапции работают по приоритету (первый сел — управляет)
- Компоненты с `IPlayerControllable` получают ввод через `ClientInput`

---

Следующий шаг: [11.03 — Пост-обработка (PostProcessing)](11_03_PostProcessing.md)
