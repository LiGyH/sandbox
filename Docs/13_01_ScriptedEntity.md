# Этап 13_01 — ScriptedEntity (Пользовательская сущность)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что это такое?

`ScriptedEntity` — это **карточка-описание** для пользовательской сущности в игре. Представьте каталог товаров в магазине: каждая карточка содержит название, описание, категорию и фотографию товара. Точно так же `ScriptedEntity` описывает одну сущность — стул, оружие, NPC — и хранит ссылку на её префаб (3D-модель с логикой).

Файл сохраняется с расширением `.sent` и появляется в меню спавна, где игроки выбирают что создать.

## Как это работает?

```
ScriptedEntity (.sent файл)
├── Prefab       → ссылка на префаб (3D-объект с компонентами)
├── Title        → название в меню ("Деревянный стул")
├── Description  → описание для игрока
├── Category     → группа в меню ("Chair", "Weapon", "Npc")
├── IncludeCode  → включить ли код при публикации в Workshop
└── Developer    → показывать только в редакторе (для тестов)
```

## Исходный код

📁 `Code/Game/Entity/ScriptedEntity.cs`

```csharp
[AssetType( Name = "Sandbox Entity", Extension = "sent", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class ScriptedEntity : GameResource, IDefinitionResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	/// <summary>
	/// Used to group this entity under a named category in the spawn menu (e.g. "Chair", "Weapon", "Npc", "World").
	/// Leave blank to place it under "Other".
	/// </summary>
	[Property]
	public string Category { get; set; }

	/// <summary>
	/// If this entity uses code then you should enable this so the code is included when publishing.
	/// </summary>
	[Property]
	public bool IncludeCode { get; set; }

	/// <summary>
	/// If true, this entity only appears in the spawn menu when running in the editor.
	/// Use for test/debug entities that shouldn't ship to players.
	/// </summary>
	[Property]
	public bool Developer { get; set; }

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		// No prefab - can't make a thumbnail
		if ( Prefab is null ) return default;

		var bitmap = new Bitmap( options.Width, options.Height );
		bitmap.Clear( Color.Transparent );

		SceneUtility.RenderGameObjectToBitmap( Prefab.GetScene(), bitmap );

		return bitmap;
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "📦", width, height, "#f54248" );
	}

	public override void ConfigurePublishing( ResourcePublishContext context )
	{
		if ( Prefab is null )
		{
			context.SetPublishingDisabled( "Invalid: missing a prefab" );
			return;
		}

		context.IncludeCode = IncludeCode;
	}
}
```

## Разбор кода

### Атрибут `[AssetType]`

```csharp
[AssetType( Name = "Sandbox Entity", Extension = "sent", Category = "Sandbox",
            Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
```

Этот атрибут регистрирует новый тип файла в редакторе s&box:

| Параметр | Что делает |
|----------|-----------|
| `Name` | Название типа ассета — «Sandbox Entity» |
| `Extension` | Расширение файла — `.sent` |
| `Category` | Категория в браузере ассетов — «Sandbox» |
| `NoEmbedding` | Нельзя встраивать в другие ресурсы |
| `IncludeThumbnails` | Автоматически генерировать превью-картинку |

### Базовый класс и интерфейс

```csharp
public class ScriptedEntity : GameResource, IDefinitionResource
```

- **`GameResource`** — базовый класс для пользовательских ресурсов (ассетов). Это не компонент на объекте в мире, а файл-описание, который можно создать в редакторе.
- **`IDefinitionResource`** — маркерный интерфейс, говорящий движку: «этот ресурс описывает что-то, что можно создавать в игре» (появляется в spawn-меню).

### Свойства

```csharp
[Property]
public PrefabFile Prefab { get; set; }
```

Главное свойство — ссылка на префаб. Префаб — это готовый игровой объект со всеми компонентами (модель, физика, логика). Когда игрок спавнит сущность, движок клонирует этот префаб.

```csharp
[Property]
public string Category { get; set; }
```

Группировка в меню спавна. Если оставить пустым — сущность попадёт в категорию «Other».

```csharp
[Property]
public bool IncludeCode { get; set; }
```

Если сущность использует C#-код (свои компоненты), нужно включить этот флаг, чтобы код попал в публикацию Workshop.

```csharp
[Property]
public bool Developer { get; set; }
```

Если `true` — сущность видна только при запуске из редактора. Полезно для отладочных объектов, которые не нужно показывать обычным игрокам.

### Генерация превью

```csharp
public override Bitmap RenderThumbnail( ThumbnailOptions options )
{
    if ( Prefab is null ) return default;

    var bitmap = new Bitmap( options.Width, options.Height );
    bitmap.Clear( Color.Transparent );

    SceneUtility.RenderGameObjectToBitmap( Prefab.GetScene(), bitmap );

    return bitmap;
}
```

Когда редактор запрашивает миниатюру для отображения в браузере ассетов:
1. Если префаб не задан — возвращает пустую картинку.
2. Создаёт прозрачный битмап нужного размера.
3. Рендерит 3D-сцену префаба в этот битмап — получается красивое превью.

### Иконка типа ассета

```csharp
protected override Bitmap CreateAssetTypeIcon( int width, int height )
{
    return CreateSimpleAssetTypeIcon( "📦", width, height, "#f54248" );
}
```

Маленькая иконка с эмодзи 📦 и красным цветом (`#f54248`), которая отображается рядом с именем файла в редакторе.

### Валидация публикации

```csharp
public override void ConfigurePublishing( ResourcePublishContext context )
{
    if ( Prefab is null )
    {
        context.SetPublishingDisabled( "Invalid: missing a prefab" );
        return;
    }

    context.IncludeCode = IncludeCode;
}
```

Перед публикацией в Workshop движок вызывает этот метод:
- Если нет префаба — публикация запрещена (нечего публиковать).
- Если всё в порядке — передаёт флаг `IncludeCode`, чтобы при необходимости включить исходный код.


---

## ➡️ Следующий шаг

Переходи к **[13.02 — Этап 13_02 — ScriptedEmitter (Ресурс эмиттера частиц)](13_02_ScriptedEmitter.md)**.
