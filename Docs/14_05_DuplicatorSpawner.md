# Этап 14_05 — DuplicatorSpawner

## Что делает этот компонент

**DuplicatorSpawner** — это система для создания дубликатов целых конструкций (групп объектов). Представьте себе **ксерокс для построек**: вы сохраняете «чертёж» нескольких связанных объектов, а затем воссоздаёте их все сразу в любом месте карты.

Он реализует интерфейс `ISpawner`, что позволяет использовать его как «полезную нагрузку» для оружия-спаунера (`SpawnerWeapon`).

---

## Полный исходный код

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Payload for spawning a duplicator contraption.
/// </summary>
public class DuplicatorSpawner : ISpawner
{
	public string DisplayName { get; private set; } = "Duplication";
	public string Icon { get; init; }
	public BBox Bounds => Dupe?.Bounds ?? default;
	public bool IsReady => Dupe is not null && _packagesReady;
	public Task<bool> Loading { get; }

	public string Data => Sandbox.Json.Serialize( new DupeInfo( Icon, Json ) );

	public DuplicationData Dupe { get; }

	public string Json { get; }

	private bool _packagesReady;

	public DuplicatorSpawner( DuplicationData dupe, string json, string name = null, string icon = null )
	{
		Dupe = dupe;
		Json = json;
		Icon = icon;
		DisplayName = name ?? "Duplication";
		Loading = InstallPackages();
	}

	/// <summary>
	/// Create from raw dupe JSON (e.g. from a storage entry). No icon.
	/// </summary>
	public static DuplicatorSpawner FromJson( string json, string name = null, string icon = null )
	{
		var dupe = Sandbox.Json.Deserialize<DuplicationData>( json );
		return new DuplicatorSpawner( dupe, json, name, icon );
	}

	/// <summary>
	/// Creates a duplicator spawner from the serialized data string. This is what gets synced to clients, so it includes the icon and raw JSON.
	/// </summary>
	/// <param name="data"></param>
	/// <returns></returns>
	public static DuplicatorSpawner FromData( string data )
	{
		var payload = Sandbox.Json.Deserialize<DupeInfo>( data );
		var dupe = Sandbox.Json.Deserialize<DuplicationData>( payload.Json );
		return new DuplicatorSpawner( dupe, payload.Json, icon: payload.Icon );
	}

	private record DupeInfo( string Icon, string Json );

	private async Task<bool> InstallPackages()
	{
		if ( Dupe?.Packages is null || Dupe.Packages.Count == 0 )
		{
			_packagesReady = true;
			return true;
		}

		foreach ( var pkg in Dupe.Packages )
		{
			if ( Cloud.IsInstalled( pkg ) )
				continue;

			await Cloud.Load( pkg );
		}

		_packagesReady = true;
		return true;
	}

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		if ( Dupe is null ) return;

		foreach ( var model in Dupe.PreviewModels )
		{
			if ( model.Model.IsError )
			{
				var bounds = model.Bounds;
				if ( bounds.Size.IsNearlyZero() ) continue;

				var t = transform.ToWorld( model.Transform );
				t = new Transform( t.PointToWorld( bounds.Center ), t.Rotation, t.Scale * (bounds.Size / 50) );
				Game.ActiveScene.DebugOverlay.Model( Model.Cube, transform: t, overlay: false, materialOveride: overrideMaterial );
			}
			else
			{
				Game.ActiveScene.DebugOverlay.Model( model.Model, transform: transform.ToWorld( model.Transform ), overlay: false, materialOveride: overrideMaterial, localBoneTransforms: model.Bones );
			}
		}
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var jsonObject = Sandbox.Json.ToNode( Dupe ) as JsonObject;
		SceneUtility.MakeIdGuidsUnique( jsonObject );

		var results = new List<GameObject>();

		using ( Game.ActiveScene.BatchGroup() )
		{
			foreach ( var entry in jsonObject["Objects"] as JsonArray )
			{
				if ( entry is not JsonObject obj )
					continue;

				var pos = entry["Position"]?.Deserialize<Vector3>() ?? default;
				var rot = entry["Rotation"]?.Deserialize<Rotation>() ?? Rotation.Identity;
				var scl = entry["Scale"]?.Deserialize<Vector3>() ?? Vector3.One;

				var world = transform.ToWorld( new Transform( pos, rot ) );
				world.Scale = scl;

				var go = new GameObject( false );
				go.Deserialize( obj, new GameObject.DeserializeOptions { TransformOverride = world } );

				Ownable.Set( go, player.Network.Owner );
				go.NetworkSpawn( true, null );

				results.Add( go );
			}
		}

		return Task.FromResult( results );
	}
}
```

---

## Разбор важных частей кода

### Свойства и состояние готовности

- `DisplayName` — отображаемое имя дубликата (по умолчанию `"Duplication"`).
- `Icon` — иконка для отображения в интерфейсе.
- `Bounds` — границы конструкции, берутся из `DuplicationData`.
- `IsReady` — спаунер готов к работе только когда данные дубликата загружены **и** все необходимые пакеты установлены (`_packagesReady`).
- `Data` — сериализованная строка, содержащая иконку и JSON-данные, для передачи по сети.

### Конструктор и фабричные методы

- **Конструктор** принимает уже десериализованные данные (`DuplicationData`), сырой JSON, имя и иконку. Сразу запускает загрузку пакетов.
- `FromJson(json)` — создаёт спаунер из «сырого» JSON (например, из сохранённой записи). Десериализует JSON в `DuplicationData` и вызывает конструктор.
- `FromData(data)` — создаёт спаунер из сетевой строки данных. Эта строка включает иконку и JSON, поэтому метод сначала извлекает `DupeInfo`, а затем десериализует основные данные.

### Загрузка зависимых пакетов (`InstallPackages`)

- Если у дубликата нет зависимых пакетов — сразу помечает `_packagesReady = true`.
- Иначе проходит по списку пакетов, проверяет, установлен ли каждый (`Cloud.IsInstalled`), и при необходимости загружает (`Cloud.Load`).
- Это **асинхронная** операция — пока пакеты не загружены, `IsReady` возвращает `false`.

### Предварительный просмотр (`DrawPreview`)

- Проходит по всем моделям в `Dupe.PreviewModels`.
- Если модель содержит ошибку (`IsError`), рисует куб-заглушку (`Model.Cube`) с размерами, соответствующими границам модели.
- Если модель корректна, рисует её через `DebugOverlay.Model` с правильной трансформацией и костями.
- Использует переданный `overrideMaterial` — это позволяет показывать прозрачный «призрак» конструкции.

### Создание объектов (`Spawn`)

- Преобразует `DuplicationData` в `JsonObject` для удобной работы.
- `SceneUtility.MakeIdGuidsUnique` — генерирует уникальные идентификаторы, чтобы не было конфликтов с уже существующими объектами.
- В цикле `BatchGroup` проходит по каждому объекту в массиве `"Objects"`:
  - Извлекает позицию, вращение и масштаб.
  - Вычисляет мировую трансформацию относительно точки размещения.
  - Создаёт новый `GameObject`, десериализует его из JSON.
  - Назначает владельца (`Ownable.Set`) и создаёт сетевой объект (`NetworkSpawn`).
- Возвращает список всех созданных объектов.

### Вспомогательная запись `DupeInfo`

- `private record DupeInfo( string Icon, string Json )` — простая структура для сериализации/десериализации пары «иконка + JSON» при передаче по сети.
