# Этап 14_03 — EntitySpawner

## Что это такое?

**EntitySpawner** — это класс, реализующий интерфейс `ISpawner`. Он отвечает за создание **скриптовых сущностей** (scripted entities) из префабов.

### Аналогия для новичков

Представьте **волшебный копировальный аппарат**: у вас есть чертёж-префаб (например, NPC, интерактивная кнопка или транспорт), и копир создаёт точную копию в игровом мире. В отличие от `PropSpawner`, который работает только с 3D-моделями, `EntitySpawner` создаёт объекты со **встроенным поведением** (скриптами).

---

## Полный исходный код

```csharp
/// <summary>
/// Payload for spawning a scripted entity prefab.
/// </summary>
public class EntitySpawner : ISpawner
{
	public string DisplayName { get; private set; }
	public string Icon => Path;
	public string Data => Path;
	public BBox Bounds { get; private set; }
	public bool IsReady => Entity is not null;
	public Task<bool> Loading { get; }

	public ScriptedEntity Entity { get; private set; }
	public string Path { get; }

	public EntitySpawner( string path )
	{
		Path = path;
		DisplayName = System.IO.Path.GetFileNameWithoutExtension( path );
		Loading = LoadAsync();
	}

	private async Task<bool> LoadAsync()
	{
		// Try local/installed first, then fall back to cloud
		Entity = await ResourceLibrary.LoadAsync<ScriptedEntity>( Path );
		Entity ??= await Cloud.Load<ScriptedEntity>( Path, true );

		if ( Entity is not null )
		{
			DisplayName = Entity.Title ?? DisplayName;
			var prefabScene = SceneUtility.GetPrefabScene( Entity.Prefab );
			Bounds = prefabScene.GetLocalBounds();
		}

		return IsReady;
	}

	/// <summary>
	/// Get a yaw correction so the longest horizontal axis of the bounds aligns with forward.
	/// </summary>
	private float GetYawCorrection()
	{
		var size = Bounds.Size;
		return size.x > size.y ? 90f : 0f;
	}

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		if ( !IsReady ) return;

		// Draw a bounding box cube as a placeholder preview
		var size = Bounds.Size;
		if ( size.IsNearlyZero() ) return;

		transform.Rotation *= Rotation.FromYaw( GetYawCorrection() );

		var center = transform.PointToWorld( Bounds.Center );
		var previewTransform = new Transform( center, transform.Rotation, transform.Scale * (size / 50) );
		Game.ActiveScene.DebugOverlay.Model( Model.Cube, transform: previewTransform, overlay: false, materialOveride: overrideMaterial );
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var depth = -Bounds.Mins.z;
		transform.Position += transform.Up * depth;
		transform.Rotation *= Rotation.FromYaw( GetYawCorrection() );

		var go = GameObject.Clone( Entity.Prefab, new CloneConfig { Transform = transform, StartEnabled = false } );
		go.Tags.Add( "removable" );

		Ownable.Set( go, player.Network.Owner );
		go.NetworkSpawn( true, null );

		return Task.FromResult( new List<GameObject> { go } );
	}
}
```

---

## Разбор важных частей

### Конструктор

- **`EntitySpawner( string path )`** — принимает путь к ресурсу скриптовой сущности.
- `DisplayName` сразу получает имя файла без расширения (через `System.IO.Path.GetFileNameWithoutExtension`).
- Запускает асинхронную загрузку `LoadAsync()`.

### Загрузка сущности (`LoadAsync`)

- Сначала ищет ресурс **локально** через `ResourceLibrary.LoadAsync<ScriptedEntity>`.
- Если не нашёл — загружает **из облака** через `Cloud.Load<ScriptedEntity>`.
- Оператор `??=` означает: «присвоить, только если текущее значение `null`».
- После загрузки вычисляет **границы** (`Bounds`) из префаб-сцены через `SceneUtility.GetPrefabScene`.

### Коррекция поворота (`GetYawCorrection`)

- Если объект **шире по оси X**, чем по оси Y, он поворачивается на 90°.
- Это нужно, чтобы длинная сторона объекта была направлена «вперёд» при размещении.

### Превью (`DrawPreview`)

- Для сущностей **нет готовой 3D-модели** для превью, поэтому рисуется **полупрозрачный куб** (`Model.Cube`) размером с ограничивающий параллелепипед.
- Масштаб куба вычисляется как `size / 50` (куб по умолчанию имеет размер 50 единиц).

### Создание объекта (`Spawn`)

- **Коррекция высоты**: `depth = -Bounds.Mins.z` — поднимает объект, чтобы он стоял на поверхности.
- **Клонирование префаба**: `GameObject.Clone( Entity.Prefab, ... )` — создаёт копию префаба с заданной трансформацией. `StartEnabled = false` означает, что объект создаётся выключенным (до сетевого спауна).
- **Тег `"removable"`** — позволяет удалить объект инструментами.
- **Владелец и сеть**: объект привязывается к игроку и появляется на сервере.

---

## Отличия от PropSpawner

| Аспект | PropSpawner | EntitySpawner |
|---|---|---|
| Что создаёт | Статичную 3D-модель | Скриптовую сущность с поведением |
| Превью | Отрисовка реальной модели | Полупрозрачный куб-заглушка |
| Создание | `new GameObject` + `Prop` | `GameObject.Clone` из префаба |
| Физика | Добавляет вручную, если нет | Уже заложена в префабе |
