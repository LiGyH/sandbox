# 🎱 Определение ховербола (HoverballDefinition)

## Что мы делаем?
Создаём ресурс-определение для ховербола (файл `.hdef`).

## Зачем это нужно?
`HoverballDefinition` описывает конкретный вариант ховербола — его префаб, название и описание. Позволяет создавать разные виды левитирующих шаров.

## Как это работает внутри движка?
- Наследуется от `GameResource`, реализует `IDefinitionResource`.
- Расширение файла — `.hdef`, категория — `Sandbox`.
- `RenderThumbnail()` рендерит префаб в миниатюру.
- `CreateAssetTypeIcon()` создаёт иконку с эмодзи бильярдного шара.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hoverball/HoverballDefinition.cs`

```csharp
﻿
[AssetType( Name = "Sandbox Hoverball", Extension = "hdef", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class HoverballDefinition : GameResource, IDefinitionResource
{
	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		if ( Prefab is null ) return default;

		var bitmap = new Bitmap( options.Width, options.Height );
		bitmap.Clear( Color.Transparent );

		SceneUtility.RenderGameObjectToBitmap( Prefab.GetScene(), bitmap );

		return bitmap;
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "🎱", width, height, "#46b4e8" );
	}
}
```

## Проверка
- Файл `.hdef` открывается в редакторе ассетов.
- Миниатюра генерируется из указанного префаба.
- Ресурс доступен в списке выбора инструмента Hoverball.
