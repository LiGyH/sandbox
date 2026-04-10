# 08.24 — Определение воздушного шара (BalloonDefinition) 🎈📋

## Что мы делаем?

Создаём ресурс `BalloonDefinition` — определение типа воздушного шара. Это `GameResource` с расширением `.bdef`, который связывает префаб шара с его метаданными (название, описание) и обеспечивает генерацию миниатюр для UI.

## Зачем это нужно?

`BalloonDefinition` позволяет:
- Определять разные типы воздушных шаров (обычный, большой, фигурный и т.д.) как ресурсы
- Отображать миниатюры шаров в интерфейсе выбора инструмента
- Поддерживать загрузку шаров из пакетов (`AllowPackages = true` в инструменте Balloon)
- Реализовать интерфейс `IDefinitionResource` для унифицированной работы с определениями

## Как это работает внутри движка?

- **`[AssetType]`** — регистрирует тип ресурса с расширением `.bdef`, категорией "Sandbox" и поддержкой миниатюр.
- **`Prefab`** — ссылка на `PrefabFile` — конкретный префаб шара, который будет клонироваться при размещении.
- **`Title` / `Description`** — метаданные для отображения в UI.
- **`RenderThumbnail()`** — рендерит 3D-модель префаба в растровое изображение для миниатюры через `SceneUtility.RenderGameObjectToBitmap`.
- **`CreateAssetTypeIcon()`** — создаёт иконку типа ассета (эмодзи 🎈 на красном фоне).

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Balloon/BalloonDefinition.cs`

```csharp
﻿

[AssetType( Name = "Sandbox Balloon", Extension = "bdef", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class BalloonDefinition : GameResource, IDefinitionResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

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
		return CreateSimpleAssetTypeIcon( "🎈", width, height, "#f54248" );
	}
}
```

## Проверка

- Класс `BalloonDefinition` наследуется от `GameResource` и реализует `IDefinitionResource`.
- Расширение файла `.bdef` зарегистрировано через `[AssetType]`.
- Флаги `NoEmbedding | IncludeThumbnails` — ресурс не встраивается в другие и поддерживает миниатюры.
- `RenderThumbnail()` проверяет наличие префаба перед рендером.
- `CreateAssetTypeIcon()` создаёт иконку с эмодзи шарика и красным фоном (`#f54248`).
- Три свойства: `Prefab`, `Title`, `Description` — всё что нужно для определения типа шара.
