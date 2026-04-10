# 15.02 — Система очистки (CleanupSystem) 🧹

## Что мы делаем?

Создаём **CleanupSystem** — систему, которая запоминает начальное состояние карты и позволяет «сбросить» сцену: удалить все заспавненные объекты и восстановить разрушенные.

## Как это работает?

1. **При загрузке сцены** — система захватывает «baseline» (снимок всех объектов с их ID и сериализованными данными)
2. **При очистке** — сравнивает текущее состояние с baseline:
   - Объекты, которых нет в baseline — удаляем (заспавненные игроками)
   - Объекты из baseline, которых нет в сцене — восстанавливаем (разрушенные)
   - Объекты игроков — не трогаем

## Создай файл

Путь: `Code/Cleanup/CleanupSystem.cs`

```csharp
using Sandbox.UI;

namespace Sandbox;

public interface ICleanupEvents
{
	public void OnCleanup( int removedObjects, int restoredObjects );
}

/// <summary>
/// A system that tracks the baseline scene state and allows resetting the map to its original state.
/// Removes all spawned props and restores destroyed map objects while leaving players untouched.
/// </summary>
public sealed class CleanupSystem : GameObjectSystem<CleanupSystem>, ISceneLoadingEvents
{
	/// <summary>
	/// Set of GameObjects that existed in the original scene baseline.
	/// </summary>
	private readonly HashSet<Guid> _baselineObjectIds = new();

	/// <summary>
	/// Serialized data of baseline objects so we can restore them if destroyed.
	/// </summary>
	private readonly Dictionary<Guid, string> _baselineObjectData = new();

	private static bool _restorePersistedBaseline;
	private static HashSet<Guid> _persistedBaselineIds;
	private static Dictionary<Guid, string> _persistedBaselineData;

	/// <summary>
	/// Whether a baseline has been captured.
	/// </summary>
	public bool HasBaseline => _baselineObjectIds.Count > 0;

	public CleanupSystem( Scene scene ) : base( scene )
	{
	}

	/// <summary>
	/// Call from SaveSystem before Game.ChangeScene() to snapshot the current baseline
	/// </summary>
	public static void PreserveBaselineForSaveLoad()
	{
		if ( Current is null || !Current.HasBaseline ) return;

		_restorePersistedBaseline = true;
		_persistedBaselineIds = new HashSet<Guid>( Current._baselineObjectIds );
		_persistedBaselineData = new Dictionary<Guid, string>( Current._baselineObjectData );
	}

	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		_baselineObjectIds.Clear();
		_baselineObjectData.Clear();
	}

	async Task ISceneLoadingEvents.OnLoad( Scene scene, SceneLoadOptions options, LoadingContext context )
	{
		await Task.Yield();

		if ( !Scene.IsValid() ) return;

		if ( _restorePersistedBaseline && _persistedBaselineIds is not null )
		{
			_baselineObjectIds.UnionWith( _persistedBaselineIds );
			foreach ( var kvp in _persistedBaselineData )
				_baselineObjectData.TryAdd( kvp.Key, kvp.Value );

			_restorePersistedBaseline = false;
		}
		else
		{
			CaptureBaseline();
		}
	}

	/// <summary>
	/// Captures the current scene state as the baseline.
	/// </summary>
	public void CaptureBaseline()
	{
		_baselineObjectIds.Clear();
		_baselineObjectData.Clear();

		foreach ( var go in Scene.Children?.ToArray() ?? [] )
		{
			CaptureObjectRecursive( go );
		}
	}

	private void CaptureObjectRecursive( GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( IsPlayerObject( go ) ) return;
		if ( go.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) ) return;

		_baselineObjectIds.Add( go.Id );

		var serialized = go.Serialize();
		if ( serialized is not null )
		{
			_baselineObjectData[go.Id] = serialized.ToJsonString();
		}

		foreach ( var child in go.Children?.ToArray() ?? [] )
		{
			CaptureObjectRecursive( child );
		}
	}

	/// <summary>
	/// Determines if a GameObject is a player or belongs to a player.
	/// </summary>
	private static bool IsPlayerObject( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		if ( go.Components.Get<Player>( true ) is not null ) return true;
		if ( go.Components.Get<PlayerData>( true ) is not null ) return true;

		var parent = go.Parent;
		while ( parent is not null && parent != go.Scene )
		{
			if ( parent.Components.Get<Player>( true ) is not null ) return true;
			if ( parent.Components.Get<PlayerData>( true ) is not null ) return true;
			parent = parent.Parent;
		}

		return false;
	}

	/// <summary>
	/// Cleans up the scene — removes spawned objects, restores destroyed baseline objects.
	/// </summary>
	public void Cleanup()
	{
		if ( !HasBaseline || !Networking.IsHost ) return;

		var removedCount = 0;
		var restoredCount = 0;
		var objectsToRemove = new List<GameObject>();
		var existingBaselineIds = new HashSet<Guid>();

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( !go.IsValid() || IsPlayerObject( go ) ) continue;
			if ( go.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) ) continue;

			if ( _baselineObjectIds.Contains( go.Id ) )
			{
				existingBaselineIds.Add( go.Id );
			}
			else
			{
				if ( go.Parent == Scene )
					objectsToRemove.Add( go );
			}
		}

		// Remove spawned objects
		foreach ( var go in objectsToRemove )
		{
			if ( go.IsValid() )
			{
				go.Destroy();
				removedCount++;
			}
		}

		// Restore destroyed baseline objects
		foreach ( var kvp in _baselineObjectData )
		{
			var id = kvp.Key;
			if ( existingBaselineIds.Contains( id ) ) continue;

			var go = Scene.Directory.FindByGuid( id );
			if ( go.IsValid() ) continue;

			try
			{
				var json = System.Text.Json.Nodes.JsonNode.Parse( kvp.Value );
				if ( json is System.Text.Json.Nodes.JsonObject jso )
				{
					var restored = new GameObject();
					restored.Deserialize( jso );
					restoredCount++;
				}
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"CleanupSystem: Failed to restore object {id}: {ex.Message}" );
			}
		}

		BroadcastCleanup( removedCount, restoredCount );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void BroadcastCleanup( int removedObjects, int restoredObjects )
	{
		Game.ActiveScene?.RunEvent<ICleanupEvents>( x => x.OnCleanup( removedObjects, restoredObjects ) );
	}

	/// <summary>
	/// Console command to cleanup the map.
	/// </summary>
	[ConCmd( "cleanup" )]
	public static void CleanupCommand( string targetName = null )
	{
		if ( !Networking.IsHost ) return;

		if ( !string.IsNullOrEmpty( targetName ) )
		{
			var target = GameManager.FindPlayerWithName( targetName );
			if ( target is not null )
				CleanupPlayer( target );
			else
				Notices.AddNotice( "cleaning_services", Color.Red, $"Can't find {targetName} to clean up" );
			return;
		}

		Current?.Cleanup();
	}

	[Rpc.Host]
	public static void RpcCleanUpMine()
	{
		CleanupPlayer( Rpc.Caller );
	}

	[Rpc.Host]
	public static void RpcCleanUpAll()
	{
		if ( !Rpc.Caller.IsHost ) return;
		Current?.Cleanup();
	}

	[Rpc.Host]
	public static void RpcCleanUpTarget( Connection target )
	{
		if ( !Rpc.Caller.IsHost ) return;
		CleanupPlayer( target );
	}

	public static void CleanupPlayer( Connection caller )
	{
		Assert.True( Networking.IsHost, "Only the host may call this method!" );

		var removable = Game.ActiveScene.GetAllComponents<Ownable>()
			.Where( o => o.Owner == caller );

		var count = 0;
		foreach ( var ownable in removable.ToArray() )
		{
			ownable.GameObject.Destroy();
			count++;
		}

		Notices.SendNotice( caller, "cleaning_services", Color.Green, $"Cleaned up {count} objects" );
	}
}
```

## Варианты очистки

| Метод | Описание |
|-------|----------|
| `Cleanup()` | Полная очистка — удаляет все заспавненные, восстанавливает разрушенные |
| `CleanupPlayer(conn)` | Только объекты конкретного игрока (по `Ownable.Owner`) |
| `RpcCleanUpMine()` | Игрок очищает свои объекты |
| `RpcCleanUpAll()` | Хост очищает всё |
| `cleanup` (консоль) | Команда хоста, опционально с именем игрока |

## Связь с SaveSystem

`PreserveBaselineForSaveLoad()` — при загрузке сохранения baseline нужно сохранить до уничтожения сцены и восстановить после загрузки новой.

---

Следующий шаг: [16.01 — Свободная камера (FreeCam)](16_01_FreeCam.md)
