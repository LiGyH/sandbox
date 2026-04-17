# 🎱 Инструмент Hoverball (HoverballTool)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что мы делаем?
Создаём режим тулгана для размещения левитирующих шаров (Hoverball) на объектах.

## Зачем это нужно?
Hoverball удерживает объект на заданной высоте, компенсируя гравитацию. Это позволяет создавать парящие конструкции и летательные аппараты.

## Как это работает внутри движка?
- Наследуется от `ToolMode`.
- Загружает определение из `.hdef`-ресурса (`HoverballDefinition`).
- ЛКМ размещает ховербол и приваривает его к объекту через `FixedJoint`.
- Если объект — мир (`point.IsWorld`), приварка не выполняется.
- Клонирует префаб с `startEnabled: false`, затем настраивает и включает через `NetworkSpawn`.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hoverball/HoverballTool.cs`

```csharp
﻿using Sandbox.UI;

[Hide]
[Title( "Hoverball" )]
[Icon( "🎱" )]
[ClassName( "hoverballtool" )]
[Group( "Building" )]
public class HoverballTool : ToolMode
{
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, ResourceSelect( Extension = "hdef", AllowPackages = true ), Title( "Hoverball" )]
	public string Definition { get; set; } = "entities/hoverball/basic.hdef";

	public override string Description => "#tool.hint.hoverballtool.description";
	public override string PrimaryAction => "#tool.hint.hoverballtool.place";

	public override bool UseSnapGrid => true;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );

		var hoverballDef = ResourceLibrary.Get<HoverballDefinition>( Definition );
		if ( hoverballDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			Spawn( select, hoverballDef.Prefab, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( hoverballDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile hoverballPrefab, Transform tx )
	{
		if ( hoverballPrefab == null )
			return;

		var go = hoverballPrefab.GetScene().Clone( global::Transform.Zero, startEnabled: false );
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		if ( !point.IsWorld )
		{
			var hoverball = go.GetComponent<HoverballEntity>( true );

			var joint = hoverball.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Hoverball";
		undo.Icon = "🎱";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}
}
```

## Проверка
- Тулган в режиме Hoverball показывает призрак шара.
- ЛКМ размещает ховербол и приваривает к объекту.
- На мировой поверхности ховербол размещается без приварки.


---

# 🎱 Сущность ховербола (HoverballEntity)

## Что мы делаем?
Создаём компонент левитирующего шара, который удерживает объект на заданной высоте.

## Зачем это нужно?
`HoverballEntity` управляет физикой левитации: отключает гравитацию, отслеживает целевую высоту и корректирует скорость `Rigidbody` для поддержания позиции по оси Z.

## Как это работает внутри движка?
- Реализует `IPlayerControllable` для управления игроком.
- `TargetZ` — целевая высота, которая изменяется входами `Up`/`Down`.
- `Toggle` включает/выключает левитацию (при выключении возвращается гравитация).
- В `OnFixedUpdate()` вычисляет разницу между текущей и целевой высотой, корректирует `Velocity.z`.
- `AirResistance` добавляет горизонтальное торможение.
- `Speed` регулирует скорость изменения высоты.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hoverball/HoverballEntity.cs`

```csharp
﻿[Alias( "hoverball" )]
public class HoverballEntity : Component, IPlayerControllable
{
	/// <summary>
	/// Is the hoverball on?
	/// </summary>
	[Property, Sync]
	public bool IsEnabled { get; private set; } = true;

	/// <summary>
	/// The world Z position the hoverball is trying to maintain.
	/// </summary>
	[Property, Sync]
	public float TargetZ { get; private set; }

	/// <summary>
	/// How fast the target height changes when inputs are held.
	/// </summary>
	[Property, Sync, ClientEditable, Range( 0, 20 )]
	public float Speed { get; set; } = 1f;

	/// <summary>
	/// Horizontal air resistance applied while hovering. Also increases vertical damping.
	/// </summary>
	[Property, Sync, ClientEditable, Range( 0, 10 )]
	public float AirResistance { get; set; } = 0f;

	/// <summary>
	/// While held, raises the hover target.
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Up { get; set; }

	/// <summary>
	/// While held, lowers the hover target.
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Down { get; set; }

	/// <summary>
	/// Toggles the hoverball on/off
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Toggle { get; set; }

	[Property]
	public GameObject OnEffect { get; set; }

	private float _zVelocity;
	private bool _toggleWasHeld;

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		TargetZ = WorldPosition.z;

		var rb = GetComponent<Rigidbody>();
		if ( rb.IsValid() )
			rb.Gravity = !IsEnabled;

		OnEffect?.Enabled = IsEnabled;
	}

	public void OnStartControl() { }

	public void OnEndControl()
	{
		_zVelocity = 0f;
	}

	public void OnControl()
	{
		var toggleHeld = Toggle.GetAnalog() > 0.5f;
		if ( toggleHeld && !_toggleWasHeld )
		{
			DoToggle();
		}

		_toggleWasHeld = toggleHeld;

		// Accumulate velocity
		var upAnalog = Up.GetAnalog();
		var downAnalog = Down.GetAnalog();
		var zDir = upAnalog - downAnalog;
		_zVelocity = zDir != 0f ? zDir * Time.Delta * 5000f : 0f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;

		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		if ( !IsEnabled ) return;

		// Shift target height from held inputs
		if ( _zVelocity != 0f )
		{
			TargetZ += _zVelocity * Time.Delta * Speed;
		}

		var pos = WorldPosition;
		var vel = rb.Velocity;
		var distance = TargetZ - pos.z;

		// Drive Z velocity toward a target proportional to distance
		var targetVelZ = Math.Clamp( distance * 20f, -400f, 400f );
		var newVelZ = vel.z + (targetVelZ - vel.z) * Math.Min( Time.Delta * 15f * (AirResistance + 1f), 1f );

		var newVel = vel.WithZ( newVelZ );

		// Horizontal air resistance
		if ( AirResistance > 0f )
		{
			var drag = Math.Min( AirResistance * Time.Delta * 5f, 1f );
			newVel = newVel.WithX( vel.x * (1f - drag) ).WithY( vel.y * (1f - drag) );
		}

		rb.Velocity = newVel;
	}

	private void DoToggle()
	{
		IsEnabled = !IsEnabled;

		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		if ( IsEnabled )
		{
			TargetZ = WorldPosition.z;
			rb.Gravity = false;
		}
		else
		{
			rb.Gravity = true;
		}

		OnEffect?.Enabled = IsEnabled;
	}
}
```

## Проверка
- Ховербол удерживает объект на начальной высоте.
- Клавиши вверх/вниз изменяют целевую высоту.
- Переключатель включает/выключает левитацию.
- Сопротивление воздуха замедляет горизонтальное движение.


---

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


---

## ➡️ Следующий шаг

Переходи к **[11.03 — 🚀 Инструмент Thruster (ThrusterTool)](11_03_Thruster.md)**.
