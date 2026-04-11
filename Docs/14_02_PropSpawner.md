# Этап 14_02 — PropSpawner

## Что это такое?

**PropSpawner** — это класс, реализующий интерфейс `ISpawner`. Он отвечает за загрузку и размещение 3D-моделей (пропсов) в игровом мире.

### Аналогия для новичков

Представьте **3D-принтер**: вы указываете ему файл модели (локальный или из облака), он загружает чертёж, показывает превью, а затем «печатает» объект прямо в игровом мире.

---

## Полный исходный код

```csharp
/// <summary>
/// Payload for spawning a prop model from a cloud ident.
/// </summary>
public class PropSpawner : ISpawner
{
	public string DisplayName { get; private set; }
	public string Icon => Path;
	public string Data => Path;
	public BBox Bounds => Model?.Bounds ?? default;
	public bool IsReady => Model is not null && !Model.IsError;
	public Task<bool> Loading { get; }

	public Model Model { get; private set; }
	public string Path { get; }

	public PropSpawner( string path )
	{
		Path = path;
		DisplayName = null;
		Loading = LoadAsync();
	}

	private async Task<bool> LoadAsync()
	{
		// Try local/installed first, then fall back to cloud
		if ( Path.EndsWith( ".vmdl" ) )
		{
			Model = await ResourceLibrary.LoadAsync<Model>( Path );
			if ( Model is not null )
			{
				DisplayName = Model.ResourceName;
				return true;
			}
		}

		Model = await Cloud.Load<Model>( Path );

		if ( Model is not null )
		{
			DisplayName = Model.ResourceName ?? DisplayName;
		}

		return IsReady;
	}

	public void DrawPreview( Transform transform, Material overrideMaterial )
	{
		if ( !IsReady ) return;

		Game.ActiveScene.DebugOverlay.Model( Model, transform: transform, overlay: false, materialOveride: overrideMaterial );
	}

	public Task<List<GameObject>> Spawn( Transform transform, Player player )
	{
		var depth = -Bounds.Mins.z;
		transform.Position += transform.Up * depth;

		var go = new GameObject( false, "prop" );
		go.Tags.Add( "removable" );
		go.WorldTransform = transform;

		var prop = go.AddComponent<Prop>();
		prop.Model = Model;

		Ownable.Set( go, player.Network.Owner );

		if ( (Model.Physics?.Parts?.Count ?? 0) == 0 )
		{
			var collider = go.AddComponent<BoxCollider>();
			collider.Scale = Model.Bounds.Size;
			collider.Center = Model.Bounds.Center;
			go.AddComponent<Rigidbody>();
		}

		go.NetworkSpawn( true, null );

		return Task.FromResult( new List<GameObject> { go } );
	}
}
```

---

## Разбор важных частей

### Конструктор

- **`PropSpawner( string path )`** — принимает путь к модели (например, `"models/chair.vmdl"` или cloud-идентификатор).
- Сразу запускает асинхронную загрузку через `LoadAsync()`.

### Загрузка модели (`LoadAsync`)

- Сначала проверяет, заканчивается ли путь на `.vmdl` — если да, пытается загрузить **локально** через `ResourceLibrary.LoadAsync<Model>`.
- Если локально не нашлось — загружает **из облака** через `Cloud.Load<Model>`.
- После успешной загрузки устанавливает `DisplayName` из имени ресурса модели.

### Проверка готовности

- **`IsReady`** — возвращает `true`, только если модель загружена (`Model is not null`) и не содержит ошибок (`!Model.IsError`).
- **`Bounds`** — берёт ограничивающий параллелепипед из загруженной модели. Если модель ещё не загружена — возвращает `default`.

### Превью (`DrawPreview`)

- Если модель готова, рисует её «призрак» с помощью `DebugOverlay.Model`.
- Параметр `overrideMaterial` позволяет наложить полупрозрачный материал, чтобы игрок видел, что это ещё не реальный объект.

### Создание объекта (`Spawn`)

- **Коррекция высоты**: `depth = -Bounds.Mins.z` — сдвигает объект вверх, чтобы он стоял на поверхности, а не «утопал» в ней.
- **Создание `GameObject`**: создаётся новый игровой объект с именем `"prop"` и тегом `"removable"` (чтобы его можно было удалить).
- **Компонент `Prop`**: добавляется и получает загруженную модель.
- **Владелец**: через `Ownable.Set` объект привязывается к игроку, который его разместил.
- **Физика**: если у модели нет собственных физических частей, автоматически добавляются `BoxCollider` и `Rigidbody` — так объект будет взаимодействовать с физикой мира.
- **Сеть**: `NetworkSpawn( true, null )` — объект появляется у всех игроков на сервере.

---

## Почему это важно?

`PropSpawner` — это основной способ размещения 3D-моделей в игре. Он обрабатывает всю цепочку: загрузка → превью → создание → физика → сеть, позволяя игрокам легко добавлять объекты в мир.
