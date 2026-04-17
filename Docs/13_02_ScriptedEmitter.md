# Этап 13_02 — ScriptedEmitter (Ресурс эмиттера частиц)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Смотрите этап 11_07

Класс `ScriptedEmitter` подробно разобран в документации **[11_07_Emitter.md](11_07_Emitter.md)**, где он рассматривается вместе с инструментом размещения `EmitterTool` и компонентом `EmitterEntity` как единая система эмиттеров.

### Краткое напоминание

`ScriptedEmitter` — это `GameResource` (файл `.semit`), описывающий эффект частиц. Он хранит ссылку на префаб с системой частиц и используется компонентом `EmitterEntity` для создания эффектов в мире.

📁 `Code/Game/Entity/ScriptedEmitter.cs`

```csharp
[AssetType( Name = "Scripted Emitter", Extension = "semit", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class ScriptedEmitter : GameResource, IDefinitionResource
{
	/// <summary>
	/// The prefab containing the particle/VFX system to emit.
	/// </summary>
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
		return CreateSimpleAssetTypeIcon( "💨", width, height, "#42b4f5" );
	}

	public override void ConfigurePublishing( ResourcePublishContext context )
	{
		if ( Prefab is null )
		{
			context.SetPublishingDisabled( "Invalid: missing a prefab" );
			return;
		}

		var scene = Prefab.GetScene();

		if ( scene.GetAllComponents<ModelRenderer>().Any() )
		{
			context.SetPublishingDisabled( "Invalid: emitter prefab must not contain a ModelRenderer" );
			return;
		}

		if ( scene.GetAllComponents<BaseCarryable>().Any() )
		{
			context.SetPublishingDisabled( "Invalid: emitter prefab must not contain a BaseCarryable" );
			return;
		}
	}
}
```

Подробный разбор каждого метода — в [11_07_Emitter.md](11_07_Emitter.md).


---

## ➡️ Следующий шаг

Переходи к **[13.03 — Этап 13_03 — DynamiteEntity (Динамит — взрывная сущность)](13_03_DynamiteEntity.md)**.
