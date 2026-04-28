# 17_02 — ClientInput: обёртка пользовательского ввода

## Что мы делаем?

Создаём структуру `ClientInput`, которая предоставляет удобный интерфейс для опроса состояния кнопок (нажата, отпущена, удерживается) конкретного игрока через его сетевое подключение (`Connection`). Структура используется системой управления (`ControlSystem`), чтобы каждый `Player` мог считывать ввод именно своего клиента — даже на сервере.

> ℹ️ **Что изменилось:** scope раньше переключали на `PlayerController`. Сейчас передаётся целиком `Player` — это даёт коду, читающему `ClientInput.Current`, доступ ко всему, что есть у игрока: `Controller`, `PlayerInventory`, `PlayerData`, `GameObject`. Никаких лишних `GetComponentInParent<Player>()`.

## Как это работает внутри движка

В s&box каждый подключённый игрок представлен объектом `Connection`. У `Connection` есть методы `Down()`, `Pressed()`, `Released()`, которые возвращают состояние привязанного действия (action) — строкового имени команды (например, `"attack1"`, `"jump"`).

`ClientInput` оборачивает этот механизм:

1. **Поле `Action`** хранит имя действия (input action), к которому привязана данная кнопка.
2. **Статическое поле `_currentState`** содержит текущий контекст: какой `Connection` и какой `Player` сейчас обрабатываются.
3. **Метод `PushScope`** устанавливает контекст перед обработкой ввода конкретного игрока и возвращает `IDisposable`, который восстанавливает предыдущий контекст при завершении (паттерн «scope guard»).
4. **Свойство `Current`** возвращает `Player`, чей scope сейчас активен (или `null` вне `using`-блока). Используется кодом «standalone»-оружий, чтобы определить, **кто** именно жмёт триггер (см. [06.01 — `BaseCarryable.EffectiveAttacker`](06_01_BaseCarryable.md) и [07.10 — RPG](07_10_Rpg.md)).
5. Методы `Down()`, `Pressed()`, `Released()` делегируют вызов к `Connection`, привязанному к текущему scope.

Таким образом, код игровой логики пишет просто `myInput.Pressed()`, а нужный `Connection` подставляется автоматически через scope.

## Путь к файлу

`Code/Game/ControlSystem/ClientInput.cs`

## Полный код

```csharp
﻿
using Sandbox.Utility;

public struct ClientInput
{
	readonly record struct State( Connection connection, Player player );

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

	internal static IDisposable PushScope( Player player )
	{
		var previousState = _currentState;
		_currentState = new State( player?.Network?.Owner, player );

		return DisposeAction.Create( () => _currentState = previousState );
	}

	/// <summary>
	/// The player currently running an <see cref="IPlayerControllable.OnControl"/> tick,
	/// or null when not inside a control scope (e.g. during regular player input).
	/// </summary>
	public static Player Current => _currentState.player;
}
```

## Разбор кода

### Вложенная запись `State`

```csharp
readonly record struct State( Connection connection, Player player );
```

`record struct` — компактная неизменяемая структура с двумя полями. Она хранит пару: сетевое подключение игрока (`Connection`) и сам объект `Player`. Модификатор `readonly` гарантирует, что экземпляр `State` нельзя изменить после создания.

### Статическое состояние

```csharp
static State _currentState;
static Connection Connection => _currentState.connection;
```

`_currentState` — глобальный (для структуры) контекст. Свойство `Connection` — короткий способ достать подключение из текущего состояния. Поскольку ввод обрабатывается последовательно (кадр за кадром), одного статического поля достаточно — конкурентного доступа нет.

### Свойство `IsEnabled`

```csharp
public readonly bool IsEnabled => !string.IsNullOrWhiteSpace( Action );
```

Если строка `Action` пуста или равна `null`, считается, что данный `ClientInput` не настроен — все методы будут возвращать `false` / `0`.

### Свойство `Action`

```csharp
public string Action { get; set; }
```

Имя input-действия, например `"attack1"`, `"jump"`, `"use"`. Оно задаётся при конфигурации контроллера игрока и соответствует привязкам клавиш в движке.

### Метод `GetAnalog()`

```csharp
public readonly float GetAnalog()
{
    if ( !IsEnabled ) return 0;
    return Down() ? 1 : 0;
}
```

Возвращает аналоговое значение от `0` до `1`. Для цифровых кнопок это просто `0` (не нажата) или `1` (нажата). Метод полезен для единообразной обработки как цифровых кнопок, так и аналоговых осей (например, триггеров геймпада).

### Метод `Down()`

```csharp
public readonly bool Down()
{
    if ( !IsEnabled ) return false;
    return Connection?.Down( Action ) ?? false;
}
```

Возвращает `true`, если кнопка **удерживается** прямо сейчас. Оператор `?.` защищает от `NullReferenceException`, если `Connection` отсутствует (например, игрок отключился), — в этом случае возвращается `false`.

### Метод `Released()`

```csharp
public readonly bool Released()
{
    if ( !IsEnabled ) return false;
    return Connection?.Released( Action ) ?? false;
}
```

Возвращает `true` **только в том кадре**, когда кнопка была отпущена. Это событие-импульс, а не состояние.

### Метод `Pressed()`

```csharp
public readonly bool Pressed()
{
    if ( !IsEnabled ) return false;
    return Connection?.Pressed( Action ) ?? false;
}
```

Возвращает `true` **только в том кадре**, когда кнопка была нажата (переход из «не нажата» → «нажата»). Используется для одиночных действий: прыжок, выстрел, переключение.

### Метод `PushScope()`

```csharp
internal static IDisposable PushScope( Player player )
{
    var previousState = _currentState;
    _currentState = new State( player?.Network?.Owner, player );

    return DisposeAction.Create( () => _currentState = previousState );
}
```

Это ключевой метод всей структуры. Он:

1. Сохраняет предыдущее состояние в локальную переменную `previousState`.
2. Устанавливает новый контекст — подключение берётся из `player.Network.Owner` (владелец сетевого объекта — это клиент, которому принадлежит данный `Player`).
3. Возвращает `IDisposable`, при вызове `Dispose()` которого восстанавливается предыдущий контекст.

### Свойство `Current`

```csharp
public static Player Current => _currentState.player;
```

Возвращает `Player`, в чьём `PushScope`-блоке мы сейчас находимся, или `null` за его пределами. Это «обратный» канал из системы управления: контрапция (например, RPG в стандалоне) внутри `OnControl` не получает игрока в параметрах — но может прочитать `ClientInput.Current` и узнать, кто на самом деле жмёт триггер. См. реальные примеры: `BaseCarryable.EffectiveAttacker` ([06.01](06_01_BaseCarryable.md)) и `RpgWeapon.CreateProjectile` ([07.10](07_10_Rpg.md)).

Типичное использование:

```csharp
using ( ClientInput.PushScope( player ) )
{
    // Здесь все вызовы myInput.Down(), myInput.Pressed() и т.д.
    // будут опрашивать подключение именно этого player'а.
}
// Здесь контекст восстановлен к прежнему.
```

Паттерн `PushScope` / `IDisposable` часто используется в s&box для временной подмены глобального контекста. Утилита `DisposeAction.Create()` из `Sandbox.Utility` оборачивает лямбду в `IDisposable` — при выходе из `using`-блока лямбда будет вызвана автоматически.

## Что проверить

1. **Компиляция** — убедитесь, что проект собирается без ошибок. Для этого нужны пространства имён `Sandbox` и `Sandbox.Utility`.
2. **Привязка действия** — установите `Action = "jump"` (или другое известное действие) и вызовите `Pressed()` внутри `PushScope` нужного игрока. В консоли выведите результат — при нажатии клавиши прыжка должно вернуться `true`.
3. **Безопасность при null** — передайте `null` в `PushScope` и вызовите `Down()`. Должен вернуться `false` без исключений.
4. **Вложенные scope** — вызовите `PushScope` для игрока A, внутри него — `PushScope` для игрока B. Убедитесь, что после выхода из внутреннего блока контекст вернулся к игроку A.
5. **Пустой Action** — создайте `ClientInput` без задания `Action`. Все методы (`Down`, `Pressed`, `Released`, `GetAnalog`) должны возвращать `false` / `0`.


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[18.01 — Пост-обработка (PostProcessing) 🎨](18_01_PostProcessResource.md)** — начало следующей фазы.
