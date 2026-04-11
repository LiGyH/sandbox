# 23_01 — Интерфейс событий сохранения (ISaveEvents)

## Что мы делаем?

Создаём интерфейс `ISaveEvents`, который позволяет любому компоненту реагировать на события системы сохранения. Компонент, реализующий этот интерфейс, получит уведомления:

- **Перед сохранением** — подготовить данные для записи.
- **После сохранения** — выполнить действия после записи файла.
- **Перед загрузкой** — очистить состояние перед перезагрузкой сцены.
- **После загрузки** — восстановить рантайм-состояние из загруженных данных.

## Как это работает внутри движка

- `ISceneEvent<T>` — базовый интерфейс s&box для событий сцены. Позволяет движку автоматически находить все компоненты, реализующие `ISaveEvents`, и вызывать их методы.
- `SaveSystem` — система сохранения, которая при операциях save/load делает широковещательную рассылку событий через `ISceneEvent`.
- Все четыре метода интерфейса имеют реализацию по умолчанию (пустое тело `{ }`) — компонент может переопределить только те, которые ему нужны.
- Параметр `filename` — имя файла сохранения, чтобы компонент знал, с каким слотом идёт работа.

## Путь к файлу

```
Code/Save/ISaveEvents.cs
```

## Полный код

### `ISaveEvents.cs`

```csharp
namespace Sandbox;

/// <summary>
/// Allows listening to events related to the <see cref="SaveSystem"/>.
/// Implement this on a <see cref="Component"/> to receive callbacks before and after saves and loads.
/// </summary>
public interface ISaveEvents : ISceneEvent<ISaveEvents>
{
	/// <summary>
	/// Called before the scene state is captured for saving.
	/// Use this to prepare any transient state that needs to be persisted.
	/// </summary>
	void BeforeSave( string filename ) { }

	/// <summary>
	/// Called after the save file has been written to disk.
	/// </summary>
	void AfterSave( string filename ) { }

	/// <summary>
	/// Called before a save file is loaded — the current scene is still active.
	/// Use this to clean up any state that won't survive the scene reload.
	/// </summary>
	void BeforeLoad( string filename ) { }

	/// <summary>
	/// Called after a save file has been loaded and the scene is fully restored.
	/// Use this to re-initialize any runtime state from the loaded data.
	/// </summary>
	void AfterLoad( string filename ) { }
}
```

## Разбор кода

### Наследование

```csharp
public interface ISaveEvents : ISceneEvent<ISaveEvents>
```

`ISceneEvent<ISaveEvents>` — generic-интерфейс, который регистрирует `ISaveEvents` в системе событий сцены. Это позволяет `SaveSystem` вызывать методы на **всех** компонентах, реализующих `ISaveEvents`, одной командой:

```csharp
// Внутри SaveSystem (примерная логика)
ISaveEvents.Post( x => x.BeforeSave( filename ) );
```

Метод `Post` автоматически находит все активные компоненты в сцене и вызывает указанный метод.

### Метод `BeforeSave`

```csharp
void BeforeSave( string filename ) { }
```

**Когда вызывается**: непосредственно перед тем, как `SaveSystem` создаст снимок состояния сцены.

**Зачем нужен**: подготовить данные, которые существуют только в рантайме и не сериализуются автоматически. Например:
- Записать текущую позицию камеры в сериализуемое свойство.
- Сохранить счётчики, кэши или другое временное состояние.

### Метод `AfterSave`

```csharp
void AfterSave( string filename ) { }
```

**Когда вызывается**: после того, как файл сохранения записан на диск.

**Зачем нужен**: выполнить действия постфактум. Например:
- Показать уведомление «Игра сохранена».
- Записать метаданные (время сохранения, скриншот).

### Метод `BeforeLoad`

```csharp
void BeforeLoad( string filename ) { }
```

**Когда вызывается**: перед загрузкой файла сохранения. Текущая сцена **ещё активна**.

**Зачем нужен**: очистить состояние, которое не переживёт перезагрузку сцены. Например:
- Остановить фоновые корутины.
- Отключить сетевые подключения.
- Сбросить синглтоны.

### Метод `AfterLoad`

```csharp
void AfterLoad( string filename ) { }
```

**Когда вызывается**: после загрузки файла и полного восстановления сцены.

**Зачем нужен**: восстановить рантайм-состояние из загруженных данных. Например:
- Перестроить кэши и индексы.
- Перезапустить фоновые системы.
- Обновить UI на основе загруженных данных.

### Реализации по умолчанию

Все методы имеют пустое тело `{ }` — это фишка C# 8+ (default interface methods). Это означает, что при реализации интерфейса вам не нужно писать все четыре метода — только те, которые вам реально нужны.

### Пример использования

```csharp
public class InventoryManager : Component, ISaveEvents
{
    [Property] public string SavedInventoryJson { get; set; }
    
    private Dictionary<string, int> _runtimeInventory = new();

    void ISaveEvents.BeforeSave( string filename )
    {
        // Сохраняем рантайм-словарь в сериализуемое свойство
        SavedInventoryJson = Json.Serialize( _runtimeInventory );
    }

    void ISaveEvents.AfterLoad( string filename )
    {
        // Восстанавливаем рантайм-словарь из загруженных данных
        _runtimeInventory = Json.Deserialize<Dictionary<string, int>>( SavedInventoryJson )
                            ?? new();
    }
}
```

В этом примере `InventoryManager`:
- Перед сохранением конвертирует рантайм-словарь в JSON-строку.
- После загрузки восстанавливает словарь из JSON.
- Не переопределяет `AfterSave` и `BeforeLoad`, потому что они не нужны.

## Что проверить

1. Создайте компонент, реализующий `ISaveEvents`, и добавьте логирование в каждый метод.
2. Сохраните игру — в консоли должны появиться сообщения `BeforeSave` → `AfterSave`.
3. Загрузите сохранение — должны появиться `BeforeLoad` → `AfterLoad`.
4. Проверьте, что `filename` содержит корректное имя файла сохранения.
5. Убедитесь, что можно реализовать только один-два метода без ошибок компиляции.
6. Проверьте, что события вызываются на **всех** компонентах в сцене, а не только на одном.
