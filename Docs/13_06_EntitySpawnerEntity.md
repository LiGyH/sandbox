# Этап 13_06 — EntitySpawnerEntity (Спаунер сущностей)

## Что это такое?

`EntitySpawnerEntity` — это компонент-фабрика, который создаёт копии других сущностей. Представьте автомат по продаже игрушек: вы нажимаете кнопку — и из него выпадает новая игрушка. А можно поставить его на таймер, и он будет выдавать игрушки каждые N секунд.

**Два режима работы:**
- 🖱️ **Ручной** — игрок нажимает клавишу → появляется сущность
- ⏲️ **Автоматический** — сущности появляются сами через заданный интервал

Спаунер также следит, чтобы не создать слишком много объектов — есть лимит одновременно существующих сущностей.

## Как это работает?

```
EntitySpawnerEntity
├── Entity          → какую сущность спавнить (ScriptedEntity)
├── SpawnInput      → клавиша ручного спавна
├── AutoSpawn       → включить автоматический режим?
├── SpawnInterval   → интервал автоспавна (1–300 секунд)
├── MaxEntities     → максимум живых сущностей (1–50)
│
├── OnUpdate()      → проверяет таймер автоспавна
├── OnControl()     → обрабатывает нажатие клавиши
└── DoSpawn()       → [Rpc.Host] создаёт сущность на сервере
```

## Исходный код

📁 `Code/Game/Entity/EntitySpawnerEntity.cs`

```csharp
using Sandbox.UI;

/// <summary>
/// A world-placed SENT that spawns another SENT at its location.
/// Can be triggered manually via player input or automatically on a timer.
/// </summary>
[Alias( "entity_spawner" )]
public class EntitySpawnerEntity : Component, IPlayerControllable
{
	/// <summary>
	/// The SENT to spawn.
	/// </summary>
	[Property, ClientEditable]
	public ScriptedEntity Entity { get; set; }

	/// <summary>
	/// Input binding that triggers a manual spawn when the player uses this entity.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Input" )]
	public ClientInput SpawnInput { get; set; }

	/// <summary>
	/// When enabled, spawns the entity automatically every <see cref="SpawnInterval"/> seconds.
	/// </summary>
	[Property, ClientEditable, Group( "Auto Spawn" )]
	public bool AutoSpawn { get; set; } = false;

	/// <summary>
	/// Seconds between automatic spawns.
	/// </summary>
	[Property, ClientEditable, Range( 1f, 300f ), Step( 1 ), Group( "Auto Spawn" )]
	public float SpawnInterval { get; set; } = 5f;

	/// <summary>
	/// Maximum number of entities spawned by this spawner allowed to exist at once.
	/// New spawns are suppressed until existing ones are destroyed.
	/// </summary>
	[Property, ClientEditable, Range( 1, 50 ), Step( 1 ), Group( "Auto Spawn" )]
	public float MaxEntities { get; set; } = 5;

	private TimeSince _timeSinceLastSpawn;
	private readonly List<WeakReference<GameObject>> _spawnedEntities = new();

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( !AutoSpawn ) return;
		if ( _timeSinceLastSpawn < SpawnInterval ) return;

		DoSpawn();
	}

	void IPlayerControllable.OnControl()
	{
		if ( SpawnInput.Pressed() )
			DoSpawn();
	}

	void IPlayerControllable.OnStartControl() { }
	void IPlayerControllable.OnEndControl() { }

	[Rpc.Host]
	private void DoSpawn()
	{
		if ( !Entity.IsValid() || Entity.Prefab is null ) return;

		// Prune destroyed entities from the tracking list
		_spawnedEntities.RemoveAll( wr => !wr.TryGetTarget( out var go ) || !go.IsValid() );

		if ( _spawnedEntities.Count >= MaxEntities ) return;

		_timeSinceLastSpawn = 0;

		var spawned = GameObject.Clone( Entity.Prefab, new CloneConfig
		{
			Transform = WorldTransform,
			StartEnabled = false,
		} );

		spawned.Tags.Add( "removable" );

		var caller = Rpc.Caller ?? GameObject.Network.Owner;
		var player = Player.FindForConnection( caller );

		Ownable.Set( spawned, caller );
		spawned.NetworkSpawn( true, null );
		spawned.Enabled = true;

		_spawnedEntities.Add( new WeakReference<GameObject>( spawned ) );

		if ( player is not null )
		{
			var undo = player.Undo.Create();
			undo.Name = $"Spawn {Entity.Title ?? Entity.ResourceName}";
			undo.Add( spawned );
		}
	}
}
```

## Разбор кода

### Атрибут класса

```csharp
[Alias( "entity_spawner" )]
public class EntitySpawnerEntity : Component, IPlayerControllable
```

- **`[Alias("entity_spawner")]`** — псевдоним для сериализации/десериализации.
- **`Component`** — живёт на объекте в сцене.
- **`IPlayerControllable`** — игрок может управлять спаунером (нажимать кнопку спавна).

### Настраиваемые свойства

```csharp
[Property, ClientEditable]
public ScriptedEntity Entity { get; set; }
```

Ссылка на `ScriptedEntity` — ресурс, описывающий какую сущность создавать. Это та самая «карточка из каталога» (см. [13_01_ScriptedEntity.md](13_01_ScriptedEntity.md)).

```csharp
[Property, Sync, ClientEditable, Group( "Input" )]
public ClientInput SpawnInput { get; set; }
```

Привязка клавиши для ручного спавна. `[Sync]` — синхронизируется по сети.

```csharp
[Property, ClientEditable, Group( "Auto Spawn" )]
public bool AutoSpawn { get; set; } = false;

[Property, ClientEditable, Range( 1f, 300f ), Step( 1 ), Group( "Auto Spawn" )]
public float SpawnInterval { get; set; } = 5f;

[Property, ClientEditable, Range( 1, 50 ), Step( 1 ), Group( "Auto Spawn" )]
public float MaxEntities { get; set; } = 5;
```

Настройки автоматического спавна:

| Свойство | По умолчанию | Описание |
|----------|-------------|----------|
| `AutoSpawn` | `false` | Включить автоспавн |
| `SpawnInterval` | 5 сек | Интервал между спавнами (1–300 сек) |
| `MaxEntities` | 5 | Максимум одновременно живых сущностей (1–50) |

`Group("Auto Spawn")` — эти свойства отображаются в отдельной секции в редакторе.

### Отслеживание созданных сущностей

```csharp
private TimeSince _timeSinceLastSpawn;
private readonly List<WeakReference<GameObject>> _spawnedEntities = new();
```

- **`TimeSince`** — структура s&box, автоматически считающая время с момента присвоения. После `_timeSinceLastSpawn = 0` она начинает расти: через 5 секунд `_timeSinceLastSpawn` вернёт `5`.
- **`List<WeakReference<GameObject>>`** — список слабых ссылок на созданные объекты. **Слабая ссылка** (`WeakReference`) не мешает сборщику мусора удалить объект. Если сущность уничтожена в игре, ссылка «протухает» и её можно убрать из списка.

### Автоматический спавн

```csharp
protected override void OnUpdate()
{
    if ( IsProxy ) return;
    if ( !AutoSpawn ) return;
    if ( _timeSinceLastSpawn < SpawnInterval ) return;

    DoSpawn();
}
```

Каждый кадр проверяем три условия:
1. **`IsProxy`** — если это клиентская копия (не сервер) → ничего не делаем (спавн — дело сервера).
2. **`!AutoSpawn`** — автоспавн выключен → ничего не делаем.
3. **`_timeSinceLastSpawn < SpawnInterval`** — ещё не прошло достаточно времени → ждём.

Если все проверки пройдены — спавним.

### Ручной спавн

```csharp
void IPlayerControllable.OnControl()
{
    if ( SpawnInput.Pressed() )
        DoSpawn();
}
```

Когда игрок управляет спаунером и нажимает назначенную клавишу — вызываем `DoSpawn()`.

### Метод `DoSpawn()` — создание сущности

```csharp
[Rpc.Host]
private void DoSpawn()
```

`[Rpc.Host]` — метод выполняется только на сервере.

**Шаг 1 — Валидация:**
```csharp
if ( !Entity.IsValid() || Entity.Prefab is null ) return;
```
Если нет ресурса сущности или у него нет префаба — выходим.

**Шаг 2 — Очистка «мёртвых» ссылок:**
```csharp
_spawnedEntities.RemoveAll( wr => !wr.TryGetTarget( out var go ) || !go.IsValid() );
```
Удаляем из списка все ссылки на уничтоженные объекты. `TryGetTarget` возвращает `false`, если объект уже собран сборщиком мусора. `!go.IsValid()` — если объект был явно уничтожён в игре.

**Шаг 3 — Проверка лимита:**
```csharp
if ( _spawnedEntities.Count >= MaxEntities ) return;
```
Если уже достигнут максимум живых сущностей — не создаём новую.

**Шаг 4 — Сброс таймера:**
```csharp
_timeSinceLastSpawn = 0;
```
Обнуляем таймер, чтобы следующий автоспавн произошёл через `SpawnInterval` секунд.

**Шаг 5 — Клонирование префаба:**
```csharp
var spawned = GameObject.Clone( Entity.Prefab, new CloneConfig
{
    Transform = WorldTransform,
    StartEnabled = false,
} );
```
Создаём копию префаба в позиции спаунера. `StartEnabled = false` — объект пока выключен, сначала настроим.

**Шаг 6 — Настройка тегов и владельца:**
```csharp
spawned.Tags.Add( "removable" );

var caller = Rpc.Caller ?? GameObject.Network.Owner;
var player = Player.FindForConnection( caller );

Ownable.Set( spawned, caller );
```
- `"removable"` — объект можно удалить инструментом Remover.
- `Rpc.Caller` — соединение игрока, вызвавшего спавн. Если вызов не от игрока (автоспавн) — берём владельца спаунера.
- `Ownable.Set` — назначает владельца объекта (для системы прав).

**Шаг 7 — Сетевой спавн и включение:**
```csharp
spawned.NetworkSpawn( true, null );
spawned.Enabled = true;
```
Объект появляется в сети для всех игроков, затем включается.

**Шаг 8 — Отслеживание:**
```csharp
_spawnedEntities.Add( new WeakReference<GameObject>( spawned ) );
```
Добавляем слабую ссылку на новую сущность в список для подсчёта.

**Шаг 9 — Регистрация отмены:**
```csharp
if ( player is not null )
{
    var undo = player.Undo.Create();
    undo.Name = $"Spawn {Entity.Title ?? Entity.ResourceName}";
    undo.Add( spawned );
}
```
Если действие было инициировано игроком, регистрируем операцию отмены. Игрок сможет нажать Undo и удалить созданную сущность. Название формируется из `Title` ресурса (или имени файла, если Title пуст).
