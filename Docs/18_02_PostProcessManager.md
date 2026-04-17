# 18_02 — PostProcessManager: менеджер пост-обработки

## Что мы делаем?

Создаём систему `PostProcessManager`, которая управляет жизненным циклом эффектов пост-обработки: загрузка из ресурсов, создание игровых объектов-эффектов, включение/выключение, предпросмотр (preview) при наведении мыши, выбор для редактирования свойств. Это «мозг» вкладки эффектов в меню спавна.

## Как это работает внутри движка

В s&box эффекты пост-обработки (цветокоррекция, виньетка, блюр и т.д.) представлены ресурсами типа `PostProcessResource`. Каждый такой ресурс содержит ссылку на префаб (`Prefab`), внутри которого лежат компоненты с настройками эффекта.

`PostProcessManager` — это `GameObjectSystem<T>`, то есть система уровня сцены. Она:

1. **Хранит словарь `_active`** — соответствие «путь к ресурсу → созданный `GameObject`». Когда эффект нужен, менеджер клонирует префаб и прикрепляет его к камере.
2. **Хранит множество `_enabled`** — какие эффекты сейчас активны (включены игроком).
3. **Поддерживает preview** — при наведении курсора на эффект в списке он временно включается; при уходе курсора — выключается обратно (если не был включён явно).
4. **Управляет выбором** (`SelectedPath`) — какой эффект сейчас выбран для редактирования свойств в панели `EffectsProperties`.

Все созданные объекты помечаются флагом `NotNetworked`, потому что пост-обработка — чисто клиентская визуальная настройка.

## Путь к файлу

`Code/Game/PostProcessing/PostProcessManager.cs`

## Полный код

```csharp
public sealed class PostProcessManager : GameObjectSystem<PostProcessManager>
{
	private readonly Dictionary<string, GameObject> _active = new();
	private readonly HashSet<string> _enabled = new();

	public string SelectedPath { get; private set; }

	public IReadOnlyList<Component> GetComponents( string resourcePath )
	{
		if ( !_active.TryGetValue( resourcePath, out var go ) || !go.IsValid() )
			return [];

		return [.. go.GetComponentsInChildren<Component>( true )];
	}

	public IReadOnlyList<Component> GetSelectedComponents()
	{
		if ( SelectedPath == null ) return [];

		if ( !_active.TryGetValue( SelectedPath, out var go ) || !go.IsValid() )
			return [];

		return [.. go.GetComponentsInChildren<Component>( true )];
	}

	public PostProcessManager( Scene scene ) : base( scene ) { }

	public bool IsEnabled( string resourcePath ) => _enabled.Contains( resourcePath );

	private void SetEnabled( string resourcePath, bool enabled )
	{
		if ( !_active.TryGetValue( resourcePath, out var go ) ) return;

		go.Enabled = enabled;

		if ( enabled ) _enabled.Add( resourcePath );
		else _enabled.Remove( resourcePath );
	}

	private void SpawnGo( string resourcePath, bool startEnabled )
	{
		var resource = ResourceLibrary.Get<PostProcessResource>( resourcePath );
		if ( resource?.Prefab is null ) return;

		var camera = Scene.Camera?.GameObject;
		if ( camera is null ) return;

		// Spawn enabled so components initialize, then disable if not wanted
		var go = GameObject.Clone( resource.Prefab, new CloneConfig { StartEnabled = true, Parent = camera } );
		go.Flags |= GameObjectFlags.NotNetworked;

		_active[resourcePath] = go;

		if ( !startEnabled )
			go.Enabled = false;
	}

	public void Select( string resourcePath )
	{
		SelectedPath = resourcePath;
		if ( !_active.ContainsKey( resourcePath ) )
			SpawnGo( resourcePath, startEnabled: false );
	}

	private string _previewPath;

	public void Preview( string resourcePath )
	{
		if ( _previewPath == resourcePath ) return;
		Unpreview();

		_previewPath = resourcePath;
		if ( IsEnabled( resourcePath ) ) return;

		if ( !_active.ContainsKey( resourcePath ) )
			SpawnGo( resourcePath, startEnabled: true );
		else
			SetEnabled( resourcePath, true );
	}

	public void Unpreview()
	{
		if ( _previewPath is null ) return;

		if ( !IsEnabled( _previewPath ) )
			SetEnabled( _previewPath, false );

		_previewPath = null;
	}

	public void Deselect()
	{
		SelectedPath = null;
	}

	public void Toggle( string resourcePath )
	{
		SelectedPath = resourcePath;

		if ( IsEnabled( resourcePath ) )
		{
			_enabled.Remove( resourcePath );
			SetEnabled( resourcePath, false );
			return;
		}

		_enabled.Add( resourcePath );

		if ( !_active.ContainsKey( resourcePath ) )
			SpawnGo( resourcePath, startEnabled: true );
		else
			SetEnabled( resourcePath, true );
	}


	public void Set( string resourcePath, bool state )
	{
		if ( state == IsEnabled( resourcePath ) ) return;

		SetEnabled( resourcePath, state );
	}

	public void Remove( string resourcePath )
	{
		if ( _active.TryGetValue( resourcePath, out var go ) )
		{
			go.Destroy();
			_active.Remove( resourcePath );
		}

		_enabled.Remove( resourcePath );

		if ( SelectedPath == resourcePath )
			SelectedPath = null;
	}
}
```

## Разбор кода

### Объявление класса

```csharp
public sealed class PostProcessManager : GameObjectSystem<PostProcessManager>
```

`GameObjectSystem<T>` — базовый класс для систем, привязанных к сцене. Движок автоматически создаёт экземпляр при загрузке сцены и предоставляет доступ через `Scene.GetSystem<PostProcessManager>()`. Модификатор `sealed` запрещает наследование — менеджер один.

### Хранилище состояния

```csharp
private readonly Dictionary<string, GameObject> _active = new();
private readonly HashSet<string> _enabled = new();
```

- `_active` — словарь: ключ — путь к ресурсу (`ResourcePath`), значение — созданный `GameObject` (клон префаба эффекта). Эффект может быть в `_active`, но при этом выключен (`.Enabled = false`).
- `_enabled` — множество путей к тем эффектам, которые пользователь явно включил. Это отделяет «создан, но выключен» от «создан и включён».

### Свойство `SelectedPath`

```csharp
public string SelectedPath { get; private set; }
```

Путь к ресурсу эффекта, который сейчас выбран для редактирования. Панель `EffectsProperties` читает это свойство, чтобы показать редактор свойств.

### Метод `GetComponents()`

```csharp
public IReadOnlyList<Component> GetComponents( string resourcePath )
{
    if ( !_active.TryGetValue( resourcePath, out var go ) || !go.IsValid() )
        return [];

    return [.. go.GetComponentsInChildren<Component>( true )];
}
```

Возвращает все компоненты эффекта (включая дочерние объекты). Синтаксис `[.. collection]` — это collection expression из C# 12, эквивалент `collection.ToArray()`. Параметр `true` в `GetComponentsInChildren` означает «включая неактивные компоненты».

### Метод `GetSelectedComponents()`

```csharp
public IReadOnlyList<Component> GetSelectedComponents()
{
    if ( SelectedPath == null ) return [];

    if ( !_active.TryGetValue( SelectedPath, out var go ) || !go.IsValid() )
        return [];

    return [.. go.GetComponentsInChildren<Component>( true )];
}
```

То же, что `GetComponents`, но для текущего выбранного эффекта. Используется панелью свойств.

### Конструктор

```csharp
public PostProcessManager( Scene scene ) : base( scene ) { }
```

Обязательный конструктор для `GameObjectSystem`. Принимает сцену и передаёт её базовому классу. Тело пустое — инициализация полей происходит через инициализаторы.

### Метод `IsEnabled()`

```csharp
public bool IsEnabled( string resourcePath ) => _enabled.Contains( resourcePath );
```

Проверяет, включён ли эффект. Простая проверка по множеству — O(1).

### Метод `SetEnabled()` (приватный)

```csharp
private void SetEnabled( string resourcePath, bool enabled )
{
    if ( !_active.TryGetValue( resourcePath, out var go ) ) return;

    go.Enabled = enabled;

    if ( enabled ) _enabled.Add( resourcePath );
    else _enabled.Remove( resourcePath );
}
```

Включает или выключает `GameObject` эффекта и синхронизирует множество `_enabled`. Если объект ещё не создан (нет в `_active`), ничего не делает.

### Метод `SpawnGo()` (приватный)

```csharp
private void SpawnGo( string resourcePath, bool startEnabled )
{
    var resource = ResourceLibrary.Get<PostProcessResource>( resourcePath );
    if ( resource?.Prefab is null ) return;

    var camera = Scene.Camera?.GameObject;
    if ( camera is null ) return;

    var go = GameObject.Clone( resource.Prefab, new CloneConfig { StartEnabled = true, Parent = camera } );
    go.Flags |= GameObjectFlags.NotNetworked;

    _active[resourcePath] = go;

    if ( !startEnabled )
        go.Enabled = false;
}
```

Создаёт объект эффекта:

1. Загружает ресурс из библиотеки.
2. Находит камеру сцены — все эффекты являются дочерними объектами камеры.
3. Клонирует префаб. Важно: `StartEnabled = true` всегда, чтобы компоненты прошли инициализацию (`OnEnabled`, `OnStart`). Если эффект не нужен активным, он отключается **после** инициализации.
4. Флаг `NotNetworked` — эффект не реплицируется по сети.
5. Сохраняет в `_active`.

### Метод `Select()`

```csharp
public void Select( string resourcePath )
{
    SelectedPath = resourcePath;
    if ( !_active.ContainsKey( resourcePath ) )
        SpawnGo( resourcePath, startEnabled: false );
}
```

Выбирает эффект для редактирования. Если объект ещё не создан — создаёт его в выключенном состоянии (чтобы панель свойств могла отобразить параметры).

### Система предпросмотра (Preview)

```csharp
private string _previewPath;

public void Preview( string resourcePath )
{
    if ( _previewPath == resourcePath ) return;
    Unpreview();

    _previewPath = resourcePath;
    if ( IsEnabled( resourcePath ) ) return;

    if ( !_active.ContainsKey( resourcePath ) )
        SpawnGo( resourcePath, startEnabled: true );
    else
        SetEnabled( resourcePath, true );
}

public void Unpreview()
{
    if ( _previewPath is null ) return;

    if ( !IsEnabled( _previewPath ) )
        SetEnabled( _previewPath, false );

    _previewPath = null;
}
```

**Preview** — временное включение эффекта при наведении мыши. Логика:

- Если эффект уже в preview — игнорируем (предотвращает мерцание).
- Вызываем `Unpreview()`, чтобы выключить предыдущий preview.
- Если эффект уже включён пользователем — ничего делать не нужно.
- Иначе — создаём или включаем объект.

**Unpreview** — выключает эффект, **только если он не был явно включён пользователем** (проверка `!IsEnabled`).

### Метод `Deselect()`

```csharp
public void Deselect()
{
    SelectedPath = null;
}
```

Сбрасывает выбор. Панель свойств покажет заглушку «Select an effect...».

### Метод `Toggle()`

```csharp
public void Toggle( string resourcePath )
{
    SelectedPath = resourcePath;

    if ( IsEnabled( resourcePath ) )
    {
        _enabled.Remove( resourcePath );
        SetEnabled( resourcePath, false );
        return;
    }

    _enabled.Add( resourcePath );

    if ( !_active.ContainsKey( resourcePath ) )
        SpawnGo( resourcePath, startEnabled: true );
    else
        SetEnabled( resourcePath, true );
}
```

Переключает эффект: если включён — выключает, если выключен — включает. Также выбирает эффект (`SelectedPath = resourcePath`), чтобы пользователь видел его свойства.

### Метод `Set()`

```csharp
public void Set( string resourcePath, bool state )
{
    if ( state == IsEnabled( resourcePath ) ) return;
    SetEnabled( resourcePath, state );
}
```

Устанавливает конкретное состояние. Если текущее состояние совпадает — ничего не делает. Используется панелью свойств для чекбокса «Enabled».

### Метод `Remove()`

```csharp
public void Remove( string resourcePath )
{
    if ( _active.TryGetValue( resourcePath, out var go ) )
    {
        go.Destroy();
        _active.Remove( resourcePath );
    }

    _enabled.Remove( resourcePath );

    if ( SelectedPath == resourcePath )
        SelectedPath = null;
}
```

Полностью удаляет эффект: уничтожает `GameObject`, убирает из всех коллекций, сбрасывает выбор, если был выбран именно этот эффект.

## Что проверить

1. **Инициализация системы** — после загрузки сцены вызовите `Scene.GetSystem<PostProcessManager>()`. Должен вернуться рабочий экземпляр.
2. **Toggle эффекта** — вызовите `Toggle("path/to/effect.spp")` дважды. Первый раз эффект включится (появится визуально), второй — выключится.
3. **Preview** — вызовите `Preview("path/to/effect.spp")`, затем `Unpreview()`. Эффект должен включиться и затем выключиться. Если перед этим эффект был включён через `Toggle`, `Unpreview` не должен его выключать.
4. **Привязка к камере** — после `Toggle` проверьте, что новый `GameObject` является дочерним объектом камеры сцены (`Scene.Camera.GameObject`).
5. **Флаг NotNetworked** — убедитесь, что созданные объекты эффектов имеют флаг `GameObjectFlags.NotNetworked` (не реплицируются по сети).
6. **Remove** — вызовите `Remove("path/to/effect.spp")`. Объект должен быть уничтожен, а `SelectedPath` сброшен, если был равен удалённому пути.


---

## ➡️ Следующий шаг

Переходи к **[18.03 — Effects UI: интерфейс управления эффектами пост-обработки](18_03_Effects_UI.md)**.
