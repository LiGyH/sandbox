# 🚀 Определение двигателя (ThrusterDefinition)

## Что мы делаем?
Создаём ресурс-определение для двигателя (файл `.tdef`), описывающий его префаб, название и описание.

## Зачем это нужно?
`ThrusterDefinition` позволяет создавать различные типы двигателей как ассеты. Каждый `.tdef`-файл описывает конкретный вариант двигателя с собственным префабом и метаданными.

## Как это работает внутри движка?
- Наследуется от `GameResource` — базового класса ресурсов движка.
- Реализует `IDefinitionResource` для интеграции с системой выбора ресурсов.
- Атрибут `AssetType` определяет расширение `.tdef` и категорию.
- `RenderThumbnail()` генерирует миниатюру из префаба.
- `CreateAssetTypeIcon()` создаёт иконку ассета с эмодзи ракеты.

## Создай файл
`Code/Weapons/ToolGun/Modes/Thruster/ThrusterDefinition.cs`

```csharp
﻿

[AssetType( Name = "Sandbox Thruster", Extension = "tdef", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class ThrusterDefinition : GameResource, IDefinitionResource
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
		return CreateSimpleAssetTypeIcon( "🚀", width, height, "#f54248" );
	}
}
```

## Проверка
- Файл `.tdef` открывается в редакторе ассетов.
- Миниатюра генерируется из указанного префаба.
- Ресурс отображается в списке выбора инструмента Thruster.
