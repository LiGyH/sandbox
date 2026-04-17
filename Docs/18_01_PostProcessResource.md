# 18.01 — Пост-обработка (PostProcessing) 🎨

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что мы делаем?

Создаём систему пост-обработки: **PostProcessResource** (ресурс-описание эффекта) и **PostProcessManager** (менеджер, управляющий включением/отключением эффектов).

## Как это работает?

Пост-обработка в s&box работает через `PostProcessVolume` — компонент, прикреплённый к камере. `PostProcessManager` — это `GameObjectSystem`, который хранит словарь `путь_к_ресурсу → GameObject_с_эффектами`.

### Поток работы

1. В редакторе создаются `.spp` ресурсы (PostProcessResource), каждый ссылается на префаб
2. UI (Effects tab) вызывает `Toggle(path)` / `Select(path)` / `Preview(path)`
3. Manager создаёт клон префаба и прикрепляет к камере
4. Включает/отключает по запросу

## Создай файл: PostProcessResource

Путь: `Code/Game/PostProcessing/PostProcessResource.cs`

```csharp
public enum PostProcessGroup
{
	Effects,
	Overlay,
	Shaders,
	Textures,
	Misc
}

[AssetType( Name = "Post Process Effect", Extension = "spp", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class PostProcessResource : GameResource, IDefinitionResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public PostProcessGroup Group { get; set; } = PostProcessGroup.Misc;

	[Property]
	public Texture Icon { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	[Property]
	public bool IncludeCode { get; set; } = true;

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		if ( Icon is null ) return default;

		return Icon.GetBitmap( 0 );
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "🎨", width, height, "#35B851" );
	}

	public override void ConfigurePublishing( ResourcePublishContext context )
	{
		if ( Prefab is null )
		{
			context.SetPublishingDisabled( "Invalid: missing a prefab" );
			return;
		}

		if ( Icon is null )
		{
			context.SetPublishingDisabled( "Invalid: missing an icon" );
			return;
		}

		context.IncludeCode = IncludeCode;
	}
}
```

### Что такое GameResource?

`GameResource` — это ресурс движка, который можно создать в редакторе (правый клик → Create). Атрибут `[AssetType]` описывает:
- `Name` — отображаемое имя в редакторе
- `Extension` — расширение файла (`.spp`)
- `Category` — папка в меню создания
- `IDefinitionResource` — маркер для системы публикации

## Создай файл: PostProcessManager

Путь: `Code/Game/PostProcessing/PostProcessManager.cs`

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

## Разбор ключевых методов

| Метод | Что делает |
|-------|-----------|
| `Toggle(path)` | Включает/выключает эффект |
| `Preview(path)` | Временно включает для предпросмотра в UI |
| `Unpreview()` | Отключает предпросмотр |
| `Select(path)` | Выбирает эффект для редактирования свойств в UI |
| `SpawnGo()` | Клонирует префаб эффекта и крепит к камере |

## Результат

После создания этих файлов:
- В редакторе можно создавать `.spp` ресурсы пост-обработки
- UI может включать/отключать/предпросматривать эффекты
- Эффекты привязываются к камере и не синхронизируются по сети (локальные)

---

Следующий шаг: [11.04 — Вспомогательные страницы (UtilityPage)](11_04_UtilityPage.md)
