# 23.02 — Система сохранения (SaveSystem) 💾

## Что мы делаем?

Создаём **SaveSystem** — систему сохранения/загрузки состояния игры. Она работает по принципу **diff** (различий): запоминает начальное состояние сцены и сохраняет только изменения.

## Принцип работы

```
Исходная сцена (baseline) → Текущее состояние → Diff (патч)
                                                    ↓
                                            Сохранение на диск
                                                    ↓
Загрузка: baseline + patch = восстановленная сцена
```

### Зачем diff, а не полное сохранение?

1. **Размер** — патч гораздо меньше полного состояния
2. **Обновления карт** — если автор карты обновил объекты, ваш сейв применит только ваши изменения к обновлённой карте
3. **Сетевые пакеты** — можно передать подключающимся игрокам

## Создай файл

Путь: `Code/Save/SaveSystem.cs`

```csharp
﻿using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sandbox;

/// <summary>
/// A saving/loading system that captures the differences between the current scene state and the original scene.
/// </summary>
public sealed class SaveSystem : GameObjectSystem<SaveSystem>, ISceneLoadingEvents
{
	private const int CurrentSaveVersion = 2;

	public static int SaveVersion => CurrentSaveVersion;
	private Dictionary<string, string> _metadata = new();
	private readonly List<LoadedSceneEntry> _loadedScenes = new();
	private bool _suppressSystemScene;

	public string LoadedSavePath { get; private set; }
	public bool HasLoadedSave => LoadedSavePath is not null;

	public SaveSystem( Scene scene ) : base( scene )
	{
	}

	/// <summary>
	/// Set metadata on the current session's save.
	/// </summary>
	public void SetMetadata( string key, string value )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			throw new ArgumentException( "Metadata key cannot be null or empty.", nameof( key ) );

		_metadata[key] = value;
	}

	/// <summary>
	/// Get a metadata value from the current session.
	/// </summary>
	public string GetMetadata( string key, string defaultValue = null )
	{
		if ( key is null ) return defaultValue;
		return _metadata.TryGetValue( key, out var value ) ? value : defaultValue;
	}

	/// <summary>
	/// Read metadata from a save file without loading the full save.
	/// </summary>
	public static IReadOnlyDictionary<string, string> GetFileMetadata( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) ) return null;
		if ( !FileSystem.Data.FileExists( path ) ) return null;

		try
		{
			var text = FileSystem.Data.ReadAllText( path );
			using var doc = JsonDocument.Parse( text );

			if ( doc.RootElement.TryGetProperty( "Metadata", out var metaElement ) )
			{
				return JsonSerializer.Deserialize<Dictionary<string, string>>( metaElement.GetRawText() );
			}

			return new Dictionary<string, string>();
		}
		catch ( Exception e )
		{
			Log.Warning( $"SaveSystem: Failed to read metadata from '{path}': {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Save current scene state to disk.
	/// </summary>
	public bool Save( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) ) return false;
		if ( !Scene.IsValid() ) return false;
		if ( _loadedScenes.Count == 0 ) return false;

		Scene.RunEvent<ISaveEvents>( x => x.BeforeSave( path ) );

		var baseline = BuildCompositeBaseline();
		if ( baseline is null ) return false;

		var current = BuildCurrentSceneJson( Scene );
		if ( current is null ) return false;

		var patch = Json.CalculateDifferences( baseline, current, GameObject.DiffObjectDefinitions );
		var sceneSources = new JsonArray();
		foreach ( var entry in _loadedScenes )
			sceneSources.Add( JsonValue.Create( entry.ResourcePath ) );

		var primarySceneFile = GetPrimarySceneFile();
		var networkOwnership = CollectNetworkOwnership( Scene );
		var syncState = CollectSyncState( Scene );
		var requiredPackages = CollectRequiredPackages( _loadedScenes, current );

		var saveData = new JsonObject
		{
			["Version"] = CurrentSaveVersion,
			["SceneId"] = Scene.Id.ToString(),
			["SceneSources"] = sceneSources,
			["SceneProperties"] = primarySceneFile is not null
				? SerializeScenePropertyDiffs( Scene, primarySceneFile ) : null,
			["Metadata"] = JsonSerializer.SerializeToNode( _metadata ),
			["Patch"] = Json.ToNode( patch ),
			["NetworkOwnership"] = networkOwnership,
			["SyncState"] = syncState,
			["RequiredPackages"] = requiredPackages,
		};

		try
		{
			var dir = Path.GetDirectoryName( path );
			if ( !string.IsNullOrEmpty( dir ) )
				FileSystem.Data.CreateDirectory( dir );

			FileSystem.Data.WriteAllText( path, saveData.ToJsonString() );
			LoadedSavePath = path;
		}
		catch ( Exception e )
		{
			Log.Warning( $"SaveSystem: Failed to write save file '{path}': {e.Message}" );
			return false;
		}

		Scene.RunEvent<ISaveEvents>( x => x.AfterSave( path ) );
		return true;
	}

	/// <summary>
	/// Load a previously saved game state.
	/// </summary>
	public async Task<bool> Load( string path )
	{
		if ( !Networking.IsHost ) return false;
		if ( string.IsNullOrWhiteSpace( path ) ) return false;
		if ( !Scene.IsValid() ) return false;
		if ( !FileSystem.Data.FileExists( path ) ) return false;

		// ... Parse save JSON, validate version, load scenes, apply patch ...
		// Full implementation follows the diff-based restore pattern
		return true;
	}

	// ... Internal methods for baseline, diffing, etc.
}
```

**Примечание**: Полная реализация `SaveSystem` содержит ~500 строк. Выше показана архитектурная суть. Копируйте полный файл из исходного кода.

## Формат файла сохранения (.json)

```json
{
  "Version": 2,
  "SceneId": "guid",
  "SceneSources": ["scenes/main.scene"],
  "SceneProperties": { /* diff свойств сцены */ },
  "Metadata": { "map": "flatgrass", "player_count": "4" },
  "Patch": { /* diff объектов */ },
  "NetworkOwnership": { /* кто чем владеет */ },
  "SyncState": { /* синхронизированные свойства */ },
  "RequiredPackages": ["package1", "package2"]
}
```

## Ключевые методы

| Метод | Описание |
|-------|----------|
| `Save(path)` | Сохраняет diff текущего состояния vs baseline |
| `Load(path)` | Загружает baseline + применяет patch |
| `SetMetadata/GetMetadata` | Произвольные данные (название карты, время) |
| `GetFileMetadata(path)` | Чтение метаданных без полной загрузки |
| `GetFileSaveVersion(path)` | Проверка совместимости версий |

---

Следующий шаг: [15.02 — Система очистки (CleanupSystem)](23_03_CleanupSystem.md)
