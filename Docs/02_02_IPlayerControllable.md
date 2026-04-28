# 02.02 — Управляемые объекты (IPlayerControllable) 🚗

## Что мы делаем?

Этот файл создаёт интерфейс `IPlayerControllable` — контракт для объектов, которыми **игрок может управлять**. Примеры: машина, турель, вертолёт, пушка на корабле.

Когда игрок садится в машину, он перестаёт управлять своим персонажем и начинает управлять машиной. Машина должна «знать», что ей теперь управляют. Именно для этого и существует `IPlayerControllable`.

Это один из самых простых интерфейсов в проекте — всего 3 метода. Но он показывает важный паттерн: **жизненный цикл управления** (control lifecycle).

## Как это работает внутри движка?

### Жизненный цикл управления

Когда игрок берёт контроль над объектом, происходит три этапа:

```
1. OnStartControl()  — игрок «сел» в объект
        ↓
2. OnControl()        — вызывается каждый кадр, пока игрок управляет
        ↓
3. OnEndControl()     — игрок «вышел» из объекта
```

Это классический паттерн **Start → Update → End**, который встречается повсюду в игровых движках:
- Компоненты в s&box: `OnStart()` → `OnUpdate()` → `OnDestroy()`.
- Анимации: `OnBegin()` → `OnTick()` → `OnFinish()`.
- Управление: `OnStartControl()` → `OnControl()` → `OnEndControl()`.

### Зачем нужен интерфейс?

Без интерфейса система управления должна была бы знать о каждом типе объекта: «если это машина — вызови машину, если это турель — вызови турель». С интерфейсом всё проще:

```csharp
// Система управления не знает, что это за объект
IPlayerControllable target = GetControllable();
target.OnStartControl();  // работает для машины, турели, лодки...
```

Это называется **полиморфизм** — один и тот же код работает с разными типами объектов.

### Где хранится файл?

Файл находится в папке `ControlSystem/` — это подсистема, отвечающая за переключение управления между игроком и объектами.

## Создай файл

Путь: `Code/Game/ControlSystem/IPlayerControllable.cs`

```csharp
public interface IPlayerControllable
{
	/// <summary>
	/// Called once when the player starts controlling this object.
	/// Default no-op; override only if you need to react to the start of control.
	/// </summary>
	public void OnStartControl() { }

	/// <summary>
	/// Called once when the player stops controlling this object.
	/// Default no-op.
	/// </summary>
	public void OnEndControl() { }

	/// <summary>
	/// Called every fixed tick while the player controls this object.
	/// Required.
	/// </summary>
	public void OnControl();

	/// <summary>
	/// Returns true if this controllable accepts control from <paramref name="player"/>.
	/// Default = true. Used to "veto" control — for example, a seat-mounted
	/// weapon refuses to be controlled by a player who already has an active
	/// weapon in their hands, so the player's own weapon input wins.
	/// </summary>
	public bool CanControl( Player player ) => true;
}
```

## Разбор кода

### Объявление интерфейса

```csharp
public interface IPlayerControllable
```
- `public` — доступен из любого файла проекта.
- `interface` — контракт (не класс). Нельзя создать экземпляр интерфейса.
- `IPlayerControllable` — имя с префиксом `I` по конвенции C#.

### Метод OnStartControl

```csharp
public void OnStartControl();
```
Вызывается **один раз**, когда игрок берёт контроль над объектом. Здесь объект должен:
- Показать UI управления (спидометр для машины, прицел для турели).
- Заблокировать обычное управление персонажем.
- Воспроизвести анимацию «посадки».

### Метод OnControl

```csharp
public void OnControl();
```
Вызывается **каждый кадр**, пока игрок управляет объектом. Здесь объект должен:
- Читать ввод игрока (WASD, мышь).
- Обновлять физику (газ, руль, поворот турели).
- Обновлять UI (скорость, боезапас).

### Метод OnEndControl

```csharp
public void OnEndControl();
```
Вызывается **один раз**, когда игрок покидает объект. Здесь объект должен:
- Скрыть UI управления.
- Разблокировать управление персонажем.
- Воспроизвести анимацию «выхода».

### Обратите внимание: реализации по умолчанию

В отличие от старой версии интерфейса, у `OnStartControl`, `OnEndControl` и `CanControl` теперь есть **тела по умолчанию** (C# default interface methods):

- `OnStartControl()` / `OnEndControl()` — пустые. Это сделано потому, что большинству контрапций (балка с трастером, плавающий пропеллер) не нужно ничего делать по событию «сел/вышел» — они просто реагируют на ввод в `OnControl()`.
- `CanControl(Player)` — возвращает `true`. Переопредели только тогда, когда нужно **отказаться** от управления (например, оружие на сиденье отдаёт приоритет, если у игрока уже есть активное оружие — см. [06.04 — BaseWeapon](06_04_BaseWeapon.md)).

> ℹ️ **Что изменилось:** раньше параметр был `PlayerController`. Сейчас сигнатура унифицирована к `Player` — это удобнее, потому что у `Player` есть и `Controller`, и `PlayerInventory`, и `PlayerData`. Внутри своей реализации обращайся к `player.GetComponent<PlayerInventory>()` или `player.Controller`, как в `BaseWeapon.CanControl` (см. [06.04](06_04_BaseWeapon.md)).

Только `OnControl()` остаётся обязательным: если объект «управляем», он должен знать, что делать каждый кадр.

### Пример реализации

Вот как машина могла бы реализовать этот интерфейс:

```csharp
public class Car : Component, IPlayerControllable
{
    public void OnStartControl()
    {
        // Показать спидометр, заблокировать персонажа
    }

    public void OnControl()
    {
        // Читать ввод: W = газ, S = тормоз, A/D = руль
    }

    public void OnEndControl()
    {
        // Скрыть спидометр, разблокировать персонажа
    }
}
```

## Результат

После создания этого файла:
- Любой объект может стать «управляемым» — достаточно реализовать `IPlayerControllable`.
- Система управления работает единообразно с машинами, турелями, лодками и любыми будущими объектами.
- Жизненный цикл `Start → Update → End` гарантирует корректную инициализацию и очистку.

> 📖 Подробнее о системе управления: [Исходный код s&box](https://github.com/LiGyH/sbox-public)

---

Следующий шаг: [02.03 — События игрока (PlayerEvent)](02_03_PlayerEvent.md)
