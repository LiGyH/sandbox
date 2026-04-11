# 🛞 Определение колеса (WheelDefinition)

## Что мы делаем?
Создаём ресурс-определение для колеса (файл `.wdef`).

## Зачем это нужно?
`WheelDefinition` описывает конкретный тип колеса — его префаб, название и описание. Разные `.wdef`-файлы позволяют иметь разные виды колёс (маленькие, большие, гоночные и т.д.).

## Как это работает внутри движка?
- Наследуется от `GameResource`, реализует `IDefinitionResource`.
- Расширение файла — `.wdef`, категория — `Sandbox`.
- `RenderThumbnail()` рендерит префаб колеса в миниатюру.
- `CreateAssetTypeIcon()` создаёт иконку с эмодзи колеса.

## Создай файл
`Code/Weapons/ToolGun/Modes/Wheel/WheelDefinition.cs`

```csharp
﻿

[AssetType( Name = "Sandbox Wheel", Extension = "wdef", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class WheelDefinition : GameResource, IDefinitionResource
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
		return CreateSimpleAssetTypeIcon( "🛞", width, height, "#f54248" );
	}
}
```

## Проверка
- Файл `.wdef` открывается в редакторе ассетов.
- Миниатюра генерируется из указанного префаба.
- Ресурс доступен в списке выбора инструмента Wheel.
