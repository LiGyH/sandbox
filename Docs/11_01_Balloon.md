# 11.01 — Воздушный шар (Balloon) 🎈

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что мы делаем?

Создаём инструмент `Balloon` — режим Tool Gun для размещения воздушных шаров. Шар можно привязать верёвкой к объекту или поставить свободно. Поддерживает настройку длины верёвки, силы подъёма, цвета и жёсткости верёвки.

## Зачем это нужно?

Воздушные шары — классический элемент песочницы:
- Поднимают объекты в воздух (отрицательная гравитация)
- Создают летающие конструкции и дирижабли
- Декоративные элементы с настраиваемым цветом
- Привязка верёвкой или свободное размещение

## Как это работает внутри движка?

- **`Definition`** — путь к ресурсу `BalloonDefinition` (.bdef), определяющему префаб шара.
- **`Length`** — длина верёвки (0–500 единиц).
- **`Force`** — масштаб гравитации для Rigidbody шара. Отрицательное значение заставляет шар подниматься вверх.
- **`Rigid`** — если `true`, верёвка натянута (без провисания), шар размещается сразу на высоте `Length`.
- **`Tint`** — цвет шара. Если белый — используется случайный цвет (`Color.Random`).
- **`OnControl()`** — показывает превью шара, `attack1` спавнит с верёвкой, `attack2` — без.
- **`Spawn()`** — RPC-метод: клонирует префаб, настраивает верёвку (`SpringJoint` + `VerletRope` + `LineRenderer`), применяет цвет и физику.
- `ApplyPhysicsProperties()` — применяет настройки физики из базового класса.

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Balloon/Balloon.cs`

```csharp
﻿using Sandbox.UI;

[Icon( "🎈" )]
[ClassName( "balloon" )]
[Group( "Building" )]
public class Balloon : ToolMode
{
	public override bool UseSnapGrid => true;
	[Property, ResourceSelect( Extension = "bdef", AllowPackages = true ), Title( "Balloon" )]
	public string Definition { get; set; } = "entities/balloon/basic.bdef";

	[Range( 0, 500 )]
	[Property, Sync]
	public float Length { get; set; } = 50.0f;

	[Range( -10, 10 )]
	[Property, Sync]
	public float Force { get; set; } = 1.0f;

	[Property, Sync]
	public bool Rigid { get; set; } = false;

	[Property, Sync]
	public Color Tint { get; set; } = Color.White;

	public override string Description => "#tool.hint.balloon.description";
	public override string PrimaryAction => "#tool.hint.balloon.place_rope";
	public override string SecondaryAction => "#tool.hint.balloon.place";

	Color _previewTint = Color.Random;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_previewTint = Color.Random;
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var placementTx = new Transform( pos.Position );

		var thrusterDef = ResourceLibrary.Get<BalloonDefinition>( Definition );
		if ( thrusterDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			Spawn( select, thrusterDef.Prefab, placementTx, true, _previewTint );
			ShootEffects( select );
			_previewTint = Color.Random;
		}
		else if ( Input.Pressed( "attack2" ) )
		{
			Spawn( select, thrusterDef.Prefab, placementTx, false, _previewTint );
			ShootEffects( select );
			_previewTint = Color.Random;
		}

		var previewTint = Tint == Color.White ? _previewTint : Tint;
		DebugOverlay.GameObject( thrusterDef.Prefab.GetScene(), transform: placementTx, castShadows: true, color: previewTint.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile thrusterPrefab, Transform tx, bool withRope, Color spawnTint )
	{
		var go = thrusterPrefab.GetScene().Clone( global::Transform.Zero, startEnabled: false );
		go.Tags.Add( "removable" );
		go.WorldTransform = Rigid && withRope ? tx.WithPosition( tx.Position + Vector3.Up * Length ) : tx;

		var tint = Tint == Color.White ? spawnTint : Tint;

		foreach ( var c in go.GetComponentsInChildren<Prop>( true ) )
		{
			c.Tint = tint;
		}

		if ( withRope )
		{
			var anchor = new GameObject( false, "anchor" );
			anchor.Parent = point.GameObject;
			anchor.LocalTransform = point.LocalTransform;

			var joint = go.AddComponent<SpringJoint>();
			joint.Body = anchor;
			joint.MinLength = Rigid ? Length : 0;
			joint.MaxLength = Length;
			joint.RestLength = Length;
			joint.Frequency = 0;
			joint.Damping = 0;
			joint.EnableCollision = true;

			var cleanup = go.AddComponent<ConstraintCleanup>();
			cleanup.Attachment = anchor;

			const float ropeWidth = 0.4f;
			var splineInterpolation = 0;
			if ( !Rigid )
			{
				var vertletRope = go.AddComponent<VerletRope>();
				vertletRope.Attachment = anchor;

				const int maxSegmentCount = 48;
				int segmentCount = Math.Min( maxSegmentCount, MathX.CeilToInt( Length / 16.0f ) );

				vertletRope.SegmentCount = segmentCount;
				vertletRope.Radius = ropeWidth;
				splineInterpolation = segmentCount > maxSegmentCount ? 8 : 4;
			}

			var lineRenderer = go.AddComponent<LineRenderer>();
			lineRenderer.Points = [go, anchor];
			lineRenderer.Width = ropeWidth;
			lineRenderer.Color = Color.White;
			lineRenderer.Lighting = true;
			lineRenderer.CastShadows = true;
			lineRenderer.SplineInterpolation = splineInterpolation;
			lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "materials/default/rope01.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
			lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;

			anchor.NetworkSpawn( true, null );
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		foreach ( var c in go.GetComponentsInChildren<Rigidbody>( true ) )
		{
			c.GravityScale = Force;
		}

		// Persist the gravity multiplier on the spawned object so it survives
		// duplication and networking (see 12.04 — PhysicalProperties).
		var props = go.GetOrAddComponent<PhysicalProperties>();
		props.GravityScale = Force;

		var undo = Player.Undo.Create();
		undo.Name = "Balloon";
		undo.Add( go );

		Player.PlayerData?.AddStat( "tool.balloon.place" );
	}
}
```

## Проверка

- Класс `Balloon` наследуется от `ToolMode` (не от `BaseConstraintToolMode`).
- `attack1` — размещение с верёвкой, `attack2` — без верёвки.
- `BalloonDefinition` загружается из ресурса `.bdef` через `ResourceLibrary`.
- Верёвка создаётся аналогично инструменту Rope: `SpringJoint` + `VerletRope` + `LineRenderer`.
- `GravityScale = Force` — отрицательное значение заставляет шар лететь вверх. Чтобы это значение **переживало дупликацию**, оно дополнительно записывается в компонент `PhysicalProperties` (см. [12.04](12_04_MassOverride.md)) — на дупе шарик поднимается так же, как оригинал.
- В жёстком режиме шар размещается на высоте `Length` сразу над точкой привязки.
- Тег `"removable"` добавляется для совместимости с инструментом Remover.
- Превью с полупрозрачным шаром (`WithAlpha(0.9f)`) и случайным цветом.


---

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


---

# 08.25 — Сущность воздушного шара (BalloonEntity) 🎈💥

## Что мы делаем?

Создаём компонент `BalloonEntity` — логику поведения воздушного шара в мире. Этот компонент обрабатывает получение урона и «лопание» шара с эффектами и звуком.

## Зачем это нужно?

`BalloonEntity` добавляет интерактивность шарам:
- Шары можно лопнуть выстрелом или любым уроном
- При лопании воспроизводится визуальный эффект (частицы) с цветом шара
- Проигрывается звук лопания
- Ведётся статистика лопнувших шаров для каждого игрока

## Как это работает внутри движка?

- **`IDamageable`** — интерфейс, позволяющий компоненту получать урон от оружия и других источников.
- **`OnDamage()`** — вызывается при получении урона. Только на хосте (`IsProxy` проверка). Записывает статистику атакующему и вызывает `Pop()`.
- **`Pop()`** — RPC-метод на хосте:
  1. Клонирует префаб эффекта (`PopEffect`) с позицией шара
  2. Красит частицы эффекта в цвет шара через `ITintable`
  3. Проигрывает звук лопания (`PopSound`), с фолбэком на стандартный звук
  4. Уничтожает `GameObject` шара
- **`[RequireComponent] Prop`** — шар обязан иметь компонент `Prop`, из которого берётся цвет (`Tint`).

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Balloon/BalloonEntity.cs`

```csharp
public class BalloonEntity : Component, Component.IDamageable
{
	[Property] public PrefabFile PopEffect { get; set; }
	[Property] public SoundEvent PopSound { get; set; }
	[RequireComponent] public Prop Prop { get; set; }

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;

		damage.Attacker?.GetComponent<Player>()?.PlayerData?.AddStat( "balloon.popped" );

		Pop();
	}

	[Rpc.Host]
	private void Pop()
	{
		if ( PopEffect.IsValid() )
		{
			var effect = GameObject.Clone( PopEffect, new CloneConfig { Transform = WorldTransform, StartEnabled = false } );

			foreach ( var tintable in effect.GetComponentsInChildren<ITintable>( true ) )
			{
				tintable.Color = Prop.Tint;
			}

			effect.NetworkSpawn( true, null );
		}

		if ( PopSound is null )
		{
			PopSound = ResourceLibrary.Get<SoundEvent>( "entities/balloon/sounds/balloon_pop.sound" );
		}

		if ( PopSound is not null )
		{
			Sound.Play( PopSound, WorldPosition );
		}

		GameObject.Destroy();
	}
}
```

## Проверка

- Класс `BalloonEntity` наследуется от `Component` и реализует `Component.IDamageable`.
- `OnDamage()` проверяет `IsProxy` — урон обрабатывается только на хосте.
- Статистика `"balloon.popped"` записывается атакующему игроку.
- `Pop()` — `[Rpc.Host]` метод, создающий эффект, звук и уничтожающий шар.
- Эффект лопания окрашивается в цвет шара через `ITintable` интерфейс.
- Фолбэк звука: если `PopSound` не задан, загружается стандартный звук из ресурсов.
- `[RequireComponent]` гарантирует наличие `Prop` компонента на объекте.


---

## ➡️ Следующий шаг

Переходи к **[11.02 — 🎱 Инструмент Hoverball (HoverballTool)](11_02_Hoverball.md)**.
