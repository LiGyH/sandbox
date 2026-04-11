# ✨ Trail — Инструмент «След» + LineDefinition

## Что мы делаем?

Создаём **два файла**:
1. `Trail.cs` — инструмент для добавления и удаления следов (`TrailRenderer`) на объектах
2. `LineDefinition.cs` — ресурс-описание типа линии (материал, прозрачность, масштаб текстуры)

## Зачем это нужно?

- Декоративные следы за движущимися объектами (ракеты, машины)
- Визуализация траектории
- Эффекты «дыма», «огня», «свечения» за объектами

## Как это работает внутри движка?

### Управление

| Кнопка | Действие |
|--------|----------|
| **ЛКМ** | Добавить след |
| **ПКМ** | Удалить след |

### Настройки (Properties)

| Свойство | Тип | По умолчанию | Описание |
|----------|-----|-------------|----------|
| `Definition` | `string` | `"entities/trails/basic.ldef"` | Ресурс линии (материал) |
| `TrailColor` | `Color` | `Color.White` | Цвет следа |
| `StartWidth` | `float` | `4.0` | Толщина в начале |
| `EndWidth` | `float` | `0.0` | Толщина в конце (0 = сходит на нет) |
| `Lifetime` | `float` | `1.0` | Время жизни следа (секунды) |
| `CastShadows` | `bool` | `false` | Отбрасывает ли тени |

### TrailRenderer

```csharp
var trail = root.AddComponent<TrailRenderer>();
trail.Color = new Gradient(
    new Gradient.ColorFrame( 0, color ),          // начало = цвет
    new Gradient.ColorFrame( 1, color.WithAlpha( 0 ) )  // конец = прозрачный
);
trail.Width = new Curve(
    new Curve.Frame( 0, startWidth ),   // начало = толще
    new Curve.Frame( 1, endWidth )      // конец = тоньше
);
trail.LifeTime = lifetime;
```

`TrailRenderer` — компонент движка, который оставляет «хвост» за объектом. Ширина и цвет задаются кривыми/градиентами.

### LineDefinition (ресурс)

```
LineDefinition (.ldef)
  ├── Title        — название
  ├── Description  — описание
  ├── Material     — материал текстуры
  ├── WorldSpace   — текстура в мировых координатах?
  ├── UnitsPerTexture — масштаб текстуры
  ├── Opaque       — непрозрачный?
  └── BlendMode    — режим смешивания
```

### ResourceSelect

```csharp
[Property, ResourceSelect( Extension = "ldef", AllowPackages = true ), Title( "Line" )]
public string Definition { get; set; } = "entities/trails/basic.ldef";
```

`ResourceSelect` — атрибут, который в редакторе показывает выбор файла с расширением `.ldef`.

### Замена существующего следа

```csharp
var existing = root.GetComponent<TrailRenderer>();
if ( existing.IsValid() )
    existing.Destroy();
```

Если у объекта уже есть след — он удаляется перед добавлением нового.

## Создай файл №1

📄 `Code/Weapons/ToolGun/Modes/Trail.cs`

```csharp
using Sandbox.UI;

[Icon( "✨" )]
[Title( "Trail" )]
[ClassName( "trail" )]
[Group( "Render" )]
public class Trail : ToolMode
{
	[Property, ResourceSelect( Extension = "ldef", AllowPackages = true ), Title( "Line" )]
	public string Definition { get; set; } = "entities/trails/basic.ldef";

	[Property, Sync]
	public Color TrailColor { get; set; } = Color.White;

	[Property, Sync, Range( 0.1f, 128.0f )]
	public float StartWidth { get; set; } = 4.0f;

	[Property, Sync, Range( 0.0f, 128.0f )]
	public float EndWidth { get; set; } = 0.0f;

	[Property, Sync, Range( 0.1f, 10.0f )]
	public float Lifetime { get; set; } = 1.0f;

	[Property, Sync]
	public bool CastShadows { get; set; } = false;

	public override string Description => "Add or remove trails from objects";
	public override string PrimaryAction => "Add Trail";
	public override string SecondaryAction => "Remove Trail";

	public override void OnControl()
	{
		var select = TraceSelect();

		IsValidState = select.IsValid() && !select.IsWorld && !select.IsPlayer;
		if ( !IsValidState )
			return;

		if ( Input.Pressed( "attack1" ) )
		{
			var lineDef = ResourceLibrary.Get<LineDefinition>( Definition );
			AddTrail( select.GameObject, TrailColor, StartWidth, EndWidth, Lifetime, CastShadows, lineDef );
			ShootEffects( select );
		}
		else if ( Input.Pressed( "attack2" ) )
		{
			RemoveTrail( select.GameObject );
			ShootEffects( select );
		}
	}

	[Rpc.Broadcast]
	private void AddTrail( GameObject go, Color color, float startWidth, float endWidth, float lifetime, bool castShadows, LineDefinition lineDef )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var root = go.Network?.RootGameObject ?? go;

		var existing = root.GetComponent<TrailRenderer>();
		if ( existing.IsValid() )
		{
			existing.Destroy();
		}

		var trail = root.AddComponent<TrailRenderer>();
		trail.Color = new Gradient( new Gradient.ColorFrame( 0, color ), new Gradient.ColorFrame( 1, color.WithAlpha( 0 ) ) );
		trail.Width = new Curve( new Curve.Frame( 0, startWidth ), new Curve.Frame( 1, endWidth ) );
		trail.LifeTime = lifetime;
		trail.CastShadows = castShadows;
		trail.Face = SceneLineObject.FaceMode.Camera;
		trail.Opaque = lineDef.Opaque;
		trail.BlendMode = lineDef.BlendMode;

		if ( lineDef.IsValid() && lineDef.Material.IsValid() )
		{
			trail.Texturing = trail.Texturing with
			{
				Material = lineDef.Material,
				WorldSpace = lineDef.WorldSpace,
				UnitsPerTexture = lineDef.UnitsPerTexture
			};
		}
	}

	[Rpc.Broadcast]
	private void RemoveTrail( GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var root = go.Network?.RootGameObject ?? go;

		var trail = root.GetComponent<TrailRenderer>();
		if ( trail.IsValid() )
		{
			trail.Destroy();
		}
	}
}
```

## Создай файл №2

📄 `Code/Weapons/ToolGun/Modes/Trail/LineDefinition.cs`

```csharp
using Sandbox.Rendering;

[AssetType( Name = "Sandbox Line", Extension = "ldef", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class LineDefinition : GameResource, IDefinitionResource
{
	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	[Property, Group( "Material" )]
	public Material Material { get; set; }

	[Property, Group( "Material" )]
	public bool WorldSpace { get; set; } = true;

	[Property, Range( 1, 128 ), Group( "Material" )]
	public float UnitsPerTexture { get; set; } = 32.0f;

	[Property, Group( "Material" )]
	public bool Opaque { get; set; } = true;

	[Property, Group( "Material" )]
	public BlendMode BlendMode { get; set; }

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		if ( !Material.IsValid() )
		{
			// No material, but return a blank white texture instead of nothing.
			var blank = new Bitmap( options.Width, options.Height );
			blank.Clear( Color.White );
			return blank;
		}

		var texture = Material.GetTexture( "g_tColor" );
		if ( texture is null )return default;

		var bitmap = new Bitmap( options.Width, options.Height );
		bitmap.Clear( Color.Transparent );

		bitmap = bitmap.Resize( texture.Width, texture.Height );
		bitmap.DrawBitmap( texture.GetBitmap( 0 ), new Rect( 0, 0, texture.Width, texture.Height ) );

		return bitmap;
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "✨", width, height, "#48c0f5" );
	}
}
```

## Про IDefinitionResource

`IDefinitionResource` — интерфейс-маркер для ресурсов, используемых в инструментах. Используется в UI для выбора ресурса через `ResourceSelect`.

## Что проверить?

1. ЛКМ на движущийся объект → за ним тянется след
2. Изменить цвет/ширину → повторно ЛКМ → след обновляется
3. ПКМ → след удаляется
4. Создать `.ldef` файл в Assets для разных типов следов

---

➡️ Следующий шаг: [10_13 — Decal (декали)](10_13_Decal.md)
