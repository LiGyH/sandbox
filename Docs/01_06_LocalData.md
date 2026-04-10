# 01.06 — Локальное хранилище данных (LocalData)

## Что мы делаем?

Этот файл создаёт простую систему для **сохранения и чтения локальных данных** на компьютере игрока. Это как «блокнот», куда игра записывает настройки, прогресс, статистику — всё, что нужно запомнить между сессиями.

Данные сохраняются в формате JSON (текстовые файлы) в специальной папке данных игры. Четыре операции: записать (`Set`), прочитать (`Get`), проверить наличие (`Has`), удалить (`Delete`).

## Как это работает внутри движка?

### `FileSystem.Data`

В s&box `FileSystem.Data` — это виртуальная файловая система, указывающая на папку данных текущей игры. Движок сам управляет путями — вам не нужно знать точное расположение на диске.

### JSON-сериализация

- `WriteJson(path, value)` — превращает объект C# в текст JSON и сохраняет в файл.
- `ReadJson<T>(path)` — читает JSON-файл и превращает обратно в объект типа `T`.

### Обобщения (Generics)

Методы `Set<T>` и `Get<T>` используют **обобщения**: вы можете сохранять и читать данные любого типа — `int`, `string`, `List<string>`, свои классы.

## Создай файл

Путь: `Code/Utility/LocalData.cs`

```csharp

/// <summary>
/// Quick data folder file storage, good for saving local data
/// </summary>
public static class LocalData
{
	/// <summary>
	/// Serialize <paramref name="value"/> and write it to <c>{key}.json</c> under <see cref="FileSystem.Data"/>.
	/// The directory hierarchy is created automatically.
	/// </summary>
	public static void Set<T>( string key, T value )
	{
		var path = KeyToPath( key );
		var dir = System.IO.Path.GetDirectoryName( path );

		if ( !string.IsNullOrEmpty( dir ) && !FileSystem.Data.DirectoryExists( dir ) )
			FileSystem.Data.CreateDirectory( dir );

		FileSystem.Data.WriteJson( path, value );
	}

	/// <summary>
	/// Read and deserialize the value stored at <paramref name="key"/>.
	/// Returns <paramref name="fallback"/> if the file doesn't exist or deserialization fails.
	/// </summary>
	public static T Get<T>( string key, T fallback = default )
	{
		var path = KeyToPath( key );

		if ( !FileSystem.Data.FileExists( path ) )
			return fallback;

		try
		{
			return FileSystem.Data.ReadJson<T>( path );
		}
		catch ( Exception ex )
		{
			Log.Warning( ex, $"[LocalData] Failed to read '{path}'" );
			return fallback;
		}
	}

	/// <summary>
	/// Returns true if a value has been stored at <paramref name="key"/>.
	/// </summary>
	public static bool Has( string key ) => FileSystem.Data.FileExists( KeyToPath( key ) );

	/// <summary>
	/// Delete the value stored at <paramref name="key"/>. No-op if it doesn't exist.
	/// </summary>
	public static void Delete( string key )
	{
		var path = KeyToPath( key );
		if ( FileSystem.Data.FileExists( path ) )
			FileSystem.Data.DeleteFile( path );
	}

	static string KeyToPath( string key ) => $"{key}.json";
}
```

## Разбор кода

### Вспомогательный метод `KeyToPath`

```csharp
static string KeyToPath( string key ) => $"{key}.json";
```
Преобразует ключ (например, `"settings/audio"`) в путь файла (`"settings/audio.json"`). Простая строковая интерполяция.

### Метод `Set<T>` — сохранение

```csharp
public static void Set<T>( string key, T value )
{
    var path = KeyToPath( key );
    var dir = System.IO.Path.GetDirectoryName( path );

    if ( !string.IsNullOrEmpty( dir ) && !FileSystem.Data.DirectoryExists( dir ) )
        FileSystem.Data.CreateDirectory( dir );

    FileSystem.Data.WriteJson( path, value );
}
```
1. Преобразуем ключ в путь файла.
2. Извлекаем директорию из пути (например, `"settings"` из `"settings/audio.json"`).
3. Если директория не существует — создаём её.
4. Сериализуем `value` в JSON и записываем в файл.

**Пример:**
```csharp
LocalData.Set( "player/name", "Вася" );
// Создаёт файл: player/name.json с содержимым "Вася"
```

### Метод `Get<T>` — чтение

```csharp
public static T Get<T>( string key, T fallback = default )
```
- `fallback = default` — значение по умолчанию, если файл не найден или повреждён.

```csharp
if ( !FileSystem.Data.FileExists( path ) )
    return fallback;
```
Файл не существует? Возвращаем fallback.

```csharp
try
{
    return FileSystem.Data.ReadJson<T>( path );
}
catch ( Exception ex )
{
    Log.Warning( ex, $"[LocalData] Failed to read '{path}'" );
    return fallback;
}
```
Пробуем прочитать и десериализовать JSON. Если что-то пошло не так (файл повреждён, структура изменилась) — логируем предупреждение и возвращаем fallback. **Это защитное программирование** — игра не упадёт из-за битого файла.

**Пример:**
```csharp
var name = LocalData.Get<string>( "player/name", "Гость" );
// Если файл есть — вернёт "Вася", если нет — "Гость"
```

### Метод `Has` — проверка

```csharp
public static bool Has( string key ) => FileSystem.Data.FileExists( KeyToPath( key ) );
```
Однострочный метод: проверяет, существует ли файл с данными.

### Метод `Delete` — удаление

```csharp
public static void Delete( string key )
{
    var path = KeyToPath( key );
    if ( FileSystem.Data.FileExists( path ) )
        FileSystem.Data.DeleteFile( path );
}
```
Удаляет файл, если он существует. Если файла нет — ничего не делает (no-op).

## Результат

После создания этого файла:
- Проект может сохранять любые данные локально: `LocalData.Set("key", value)`.
- Чтение безопасно: при ошибке возвращается значение по умолчанию.
- Данные хранятся в JSON — легко читать и отлаживать.
- Поддерживаются вложенные директории в ключах.

---

Следующий шаг: [01.07 — Настройка камеры (CameraSetup)](01_07_CameraSetup.md)
