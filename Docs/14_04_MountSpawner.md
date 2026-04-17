# Этап 14_04 — MountSpawner

## Что это такое?

**MountSpawner** — это класс, реализующий интерфейс `ISpawner`. Он размещает 3D-модели из **смонтированных игр** (например, Half-Life 2, Counter-Strike и других Source-игр).

### Аналогия для новичков

Представьте, что вы **одалживаете игрушки из коллекции друга**. Если у вас есть эта игра — вы видите настоящую модель. Если нет — вместо неё отображается **куб-заглушка** с предложением установить нужную игру. Это и есть суть `MountSpawner`: он работает с контентом, который может быть не у всех игроков.

---

## Полный исходный код

```csharp
/// <summary>
/// Like <see cref="PropSpawner"/>, but attaches <see cref="MountMetadata"/> to the spawned object
/// so clients without the mount installed can show a fallback cube and install prompt.
/// </summary>
public class MountSpawner : ISpawner
{
	record Metadata( string GameTitle );

	public string DisplayName { get; private set; }
	public string Icon => Path;
	public string Data => Path;
	public BBox Bounds => Model.IsValid() ? Model.Bounds : default;
	public bool IsReady => Model.IsValid();
	public Task<bool> Loading { get; }

	public Model Model { get; private set; }
	public string Path { get; }

	readonly Metadata _meta;

	public MountSpawner( string path, string metadataJson )
	{
		Path = path;

		if ( string.IsNullOrEmpty( metadataJson ) )
		{
			Log.Warning( $"[MountSpawner] No metadata JSON for '{path}'" );
			_meta = new Metadata( string.Empty );
		}
		else
		{
			_meta = Json.Deserialize<Metadata>( metadataJson );
			if ( _meta is null )
				Log.Warning( $"[MountSpawner] Failed to deserialize metadata for '{path}': {metadataJson}" );
			_meta ??= new Metadata( string.Empty );
		}

		DisplayName = System.IO.Path.GetFileNameWithoutExtension( path );
		Loading = LoadAsync();
	}

	private async Task<bool> LoadAsync()
	{
		Model = await ResourceLibrary.LoadAsync<Model>( Path );
		Log.Info( $"[MountSpawner] path='{Path}' model={(Model.IsValid() ? "loaded" : "missing")} title='{_meta.GameTitle}'" );
		return true; // missing model uses placeholder
	}

	/// <summary>Serialize mount metadata to pass through the Spawn RPC.</summary>
	public static string SerializeMetadata( string gameTitle )
		=> Json.Serialize( new Metadata( gameTitle ) );

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		var bounds = Bounds;
		var t = transform;
		t = new Transform( t.PointToWorld( bounds.Center ), t.Rotation, t.Scale * ( bounds.Size / 50f ) );
		Game.ActiveScene.DebugOverlay.Model( Model.IsValid() ? Model : Model.Cube, transform: t, overlay: false, materialOveride: overrideMaterial );
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var effectiveBounds = Model.IsValid() ? Model.Bounds : new BBox( -Vector3.One * 8f, Vector3.One * 8f );
		var depth = Model.IsValid() ? -Model.Bounds.Mins.z : effectiveBounds.Size.z / 2f;
		transform.Position += transform.Up * depth;

		var go = new GameObject( false, "prop" );
		go.Tags.Add( "removable" );
		go.WorldTransform = transform;

		if ( Model.IsValid() )
		{
			var prop = go.AddComponent<Prop>();
			prop.Model = Model;

			if ( (Model.Physics?.Parts?.Count ?? 0) == 0 )
			{
				var collider = go.AddComponent<BoxCollider>();
				collider.Scale = Model.Bounds.Size;
				collider.Center = Model.Bounds.Center;
				go.AddComponent<Rigidbody>();
			}
		}
		else
		{
			var collider = go.AddComponent<BoxCollider>();
			collider.Scale = effectiveBounds.Size;
			collider.Center = effectiveBounds.Center;
			go.AddComponent<Rigidbody>();
		}

		var meta = go.AddComponent<MountMetadata>();
		meta.GameTitle = _meta.GameTitle;
		meta.BoundsSize = effectiveBounds.Size;
		meta.BoundsCenter = effectiveBounds.Center;

		Ownable.Set( go, player.Network.Owner );
		go.NetworkSpawn( true, null );

		return Task.FromResult( new List<GameObject> { go } );
	}
}
```

---

## Разбор важных частей

### Внутренний record `Metadata`

- `record Metadata( string GameTitle )` — простая структура данных, хранящая название игры-источника (например, `"Half-Life 2"`).
- Передаётся в формате JSON, чтобы другие клиенты знали, из какой игры взят объект.

### Конструктор

- **`MountSpawner( string path, string metadataJson )`** — принимает путь к модели **и** JSON с метаданными.
- Если JSON пустой или не удаётся десериализовать — создаётся «пустой» объект `Metadata` с пустой строкой, и выводится предупреждение в лог.
- Оператор `??=` гарантирует, что `_meta` никогда не будет `null`.

### Загрузка модели (`LoadAsync`)

- Загружает модель **только локально** через `ResourceLibrary.LoadAsync<Model>`.
- **Всегда возвращает `true`** — даже если модель не найдена. Это ключевое отличие: отсутствующая модель не считается ошибкой, вместо неё используется куб-заглушка.

### Сериализация метаданных (`SerializeMetadata`)

- Статический метод для упаковки названия игры в JSON.
- Используется при передаче данных через сетевой RPC (удалённый вызов процедуры).

### Превью (`DrawPreview`)

- Если модель загружена — рисует **реальную модель**.
- Если нет — рисует **куб** (`Model.Cube`) как заглушку.
- Масштаб вычисляется как `bounds.Size / 50f` (стандартный куб — 50 единиц).

### Создание объекта (`Spawn`)

- **Эффективные границы**: если модель есть — используются её реальные bounds. Если нет — создаётся куб размером 16×16×16 (`-Vector3.One * 8f` до `Vector3.One * 8f`).
- **Две ветки создания**:
  - **Модель загружена**: создаёт `Prop` с реальной моделью (как `PropSpawner`).
  - **Модель отсутствует**: создаёт `BoxCollider` + `Rigidbody` — физический куб-заглушку.
- **`MountMetadata`** — специальный компонент, который прикрепляется к объекту и хранит:
  - `GameTitle` — название игры-источника.
  - `BoundsSize` и `BoundsCenter` — размеры для отображения заглушки на клиентах без этой игры.

### Владелец и сеть

- `Ownable.Set` — привязывает объект к игроку.
- `NetworkSpawn` — объект появляется у всех игроков на сервере.

---

## Отличия от PropSpawner

| Аспект | PropSpawner | MountSpawner |
|---|---|---|
| Источник модели | Облако или локальные ресурсы | Только смонтированные игры |
| Метаданные | Нет | JSON с названием игры |
| Отсутствие модели | Считается ошибкой (`IsReady = false`) | Нормальная ситуация (используется заглушка) |
| Компонент | Только `Prop` | `Prop` + `MountMetadata` |
| Превью без модели | Не рисуется | Рисуется куб-заглушка |

---

## Почему это важно?

`MountSpawner` решает реальную проблему мультиплеера: не у всех игроков установлены одинаковые игры. Благодаря `MountMetadata` клиенты без нужной игры видят заглушку и могут установить недостающий контент, а не наблюдают «невидимые» объекты.


---

## ➡️ Следующий шаг

Переходи к **[14.05 — Этап 14_05 — DuplicatorSpawner](14_05_DuplicatorSpawner.md)**.
