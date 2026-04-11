# 🛞 Инструмент Wheel (WheelTool)

## Что мы делаем?
Создаём режим тулгана для размещения колёс на объектах.

## Зачем это нужно?
`WheelTool` позволяет игрокам прикреплять колёса к конструкциям для создания транспортных средств. Колёса имеют подвеску и могут вращаться.

## Как это работает внутри движка?
- Наследуется от `ToolMode`.
- Загружает определение колеса (`WheelDefinition`) из файла `.wdef`.
- Рассчитывает смещение от поверхности на основе размера модели колеса.
- ПКМ переключает ось между `Vector3.Right` и `Vector3.Up`.
- ЛКМ создаёт колесо с `WheelJoint` (шарнирное соединение с подвеской).
- Показывает линии отладки для оси вращения и оси подвески.

## Создай файл
`Code/Weapons/ToolGun/Modes/Wheel/WheelTool.cs`

```csharp
﻿﻿
using Sandbox.UI;

[Hide]
[Title( "Wheel" )]
[Icon( "🛞" )]
[ClassName( "wheeltool" )]
[Group( "Building" )]
public class WheelTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];
	[Property, ResourceSelect( Extension = "wdef", AllowPackages = true ), Title( "Wheel" )]
	public string Definition { get; set; } = "entities/wheel/basic.wdef";

	public override string Description => "#tool.hint.wheeltool.description";
	public override string PrimaryAction => "#tool.hint.wheeltool.place";
	public override string SecondaryAction => "#tool.hint.wheeltool.toggle_axis";

	Vector3 _axis = Vector3.Right;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var def = ResourceLibrary.Get<WheelDefinition>( Definition );
		if ( def == null || def.Prefab?.GetScene() is not Scene scene ) return;

		var pos = select.WorldTransform();
		var modelBounds = scene.GetBounds();
		var surfaceOffset = modelBounds.Size.y * 0.5f;

		if ( Input.Pressed( "attack2" ) )
		{
			_axis = _axis == Vector3.Right ? Vector3.Up : Vector3.Right;
		}

		var placementTrans = new Transform( pos.Position + pos.Rotation.Forward * surfaceOffset );
		placementTrans.Rotation = Rotation.LookAt( pos.Rotation.Forward, pos.Rotation * _axis ) * new Angles( 0, 90, 0 );
		placementTrans.Scale = scene.LocalScale;

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnWheel( select, def, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( scene, transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
		DebugOverlay.Line( new Line( placementTrans.Position, placementTrans.Position + placementTrans.Right * 5 ), Color.White );

		var suspensionAxis = placementTrans.Forward * 20;
		DebugOverlay.Line( new Line( placementTrans.Position - suspensionAxis, placementTrans.Position + suspensionAxis ), Color.Green );

	}

	[Rpc.Host]
	public void SpawnWheel( SelectionPoint point, WheelDefinition def, Transform tx )
	{
		if ( def == null || def.Prefab?.GetScene() is not Scene scene ) return;

		var wheelGo = scene.Clone( new CloneConfig { StartEnabled = false } );
		wheelGo.Name = "wheel";
		wheelGo.Tags.Add( "removable" );
		wheelGo.Tags.Add( "constraint" );
		wheelGo.WorldTransform = tx;

		var we = wheelGo.GetOrAddComponent<WheelEntity>();
		var joint = wheelGo.GetComponentInChildren<WheelJoint>( true );

		if ( joint is null )
		{
			var wheelAnchor = new GameObject( true, "anchor2" );
			wheelAnchor.Parent = wheelGo;
			wheelAnchor.LocalRotation = new Angles( 0, 90, 90 );

			//var joint = jointGo.AddComponent<HingeJoint>();
			joint = wheelAnchor.AddComponent<WheelJoint>();
			joint.Attachment = Joint.AttachmentMode.Auto;
			joint.EnableSuspension = true;
			joint.EnableSuspensionLimit = true;
			joint.SuspensionLimits = new Vector2( -32, 32 );
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( wheelGo );

		joint.Body = point.GameObject;

		wheelGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Wheel";
		undo.Add( wheelGo );

		CheckContraptionStats( point.GameObject );
	}
}
```

## Проверка
- Тулган в режиме Wheel показывает призрак колеса на поверхности.
- ЛКМ размещает колесо с подвеской.
- ПКМ переключает ось размещения.
- Линии отладки показывают оси вращения и подвески.


---

# 🛞 Сущность колеса (WheelEntity)

## Что мы делаем?
Создаём компонент управления колесом — мотор, тормоз и рулевое управление.

## Зачем это нужно?
`WheelEntity` превращает физическое колесо в управляемый элемент транспорта. Игрок может ускоряться, тормозить и поворачивать через привязанные клавиши.

## Как это работает внутри движка?
- Реализует `IPlayerControllable` для получения ввода от игрока.
- Управляет `WheelJoint` — мотором вращения (`SpinMotor`) и рулевым управлением (`Steering`).
- `Forward`/`Reverse` задают скорость вращения мотора.
- `Break` включает торможение (мотор на скорости 0 с высоким крутящим моментом).
- `TurnLeft`/`TurnRight` управляют углом поворота до ±45°.
- `Speed`, `Power`, `Reversed` настраивают поведение колеса.

## Создай файл
`Code/Weapons/ToolGun/Modes/Wheel/WheelEntity.cs`

```csharp
﻿public class WheelEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 ), ClientEditable]
	public bool Reversed { get; set; } = false;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Speed { get; set; } = 0.5f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Power { get; set; } = 0.5f;

	[Property, Sync, ClientEditable]
	public ClientInput Forward { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput Reverse { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput Break { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput TurnLeft { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput TurnRight { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();
	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	public void OnControl()
	{
		var joint = GetComponentInChildren<WheelJoint>();
		if ( !joint.IsValid() ) return;

		var forward = Forward.GetAnalog();
		var reverse = Reverse.GetAnalog();
		var speed = (forward - reverse).Clamp( -1, 1 );

		if ( Break.GetAnalog() > 0.1f )
		{
			joint.EnableSpinMotor = true;
			joint.SpinMotorSpeed = 0;
			joint.MaxSpinTorque = 500000 * Power;
		}
		else if ( speed.AlmostEqual( 0.0f ) )
		{
			joint.EnableSpinMotor = false;
		}
		else
		{
			if ( Reversed ) speed = -speed;

			joint.EnableSpinMotor = true;
			joint.SpinMotorSpeed = -2000 * speed * Speed;
			joint.MaxSpinTorque = 200000 * Power;
		}

		var left = TurnLeft.GetAnalog();
		var right = TurnRight.GetAnalog();
		var dir = (right - left).Clamp( -1, 1 );

		if ( !dir.AlmostEqual( 0.0f ) )
		{
			joint.EnableSteering = true;
			joint.SteeringDampingRatio = 1.0f;
			joint.MaxSteeringTorque = 500000;
			joint.SteeringLimits = new Vector2( -45, 45 );
			joint.TargetSteeringAngle = 30 * dir;
		}
		else
		{
			joint.TargetSteeringAngle = 0;
		}

	}
}

```

## Проверка
- Колесо вращается при нажатии клавиш вперёд/назад.
- Тормоз останавливает вращение.
- Рулевое управление поворачивает колесо влево/вправо.
- Свойство `Reversed` инвертирует направление вращения.


---

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
