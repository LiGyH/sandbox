# 🚀 Инструмент Thruster (ThrusterTool)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что мы делаем?
Создаём режим тулгана для размещения двигателей (Thruster) на объектах в мире.

## Зачем это нужно?
Thruster позволяет игрокам прикреплять реактивные двигатели к объектам, создавая летающие конструкции и транспортные средства.

## Как это работает внутри движка?
- Наследуется от `ToolMode` — базового класса режимов тулгана.
- Использует `TraceSelect()` для определения точки размещения.
- Загружает определение двигателя (`ThrusterDefinition`) из ресурса.
- При нажатии ЛКМ размещает двигатель с приваркой (`FixedJoint`), при ПКМ — без приварки.
- Клавиша перезарядки переключает ось тяги между `Vector3.Up` и `Vector3.Right`.
- Показывает призрак объекта через `DebugOverlay.GameObject`.

## Создай файл
`Code/Weapons/ToolGun/Modes/Thruster/ThrusterTool.cs`

```csharp
﻿﻿using Sandbox.UI;

[Hide]
[Title( "Thruster" )]
[Icon( "🚀" )]
[ClassName( "thrustertool" )]
[Group( "Building" )]
public class ThrusterTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, ResourceSelect( Extension = "tdef", AllowPackages = true ), Title( "Thruster" )]
	public string Definition { get; set; } = "entities/thruster/basic.tdef";

	public override string Description => "#tool.hint.thrustertool.description";
	public override string PrimaryAction => "#tool.hint.thrustertool.place";
	public override string SecondaryAction => "#tool.hint.thrustertool.place_no_weld";
	public override string ReloadAction => "#tool.hint.thrustertool.toggle_axis";

	Vector3 _axis = Vector3.Up;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();

		if ( Input.Pressed( "reload" ) )
		{
			_axis = _axis == Vector3.Right ? Vector3.Up : Vector3.Right;
		}

		// Default: thrust away from surface normal. Toggle: thrust into surface (180° flip).
		var axisOffset = _axis == Vector3.Up ? new Angles( 90, 0, 0 ) : new Angles( -90, 0, 0 );

		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation * axisOffset;

		var thrusterDef = ResourceLibrary.Get<ThrusterDefinition>( Definition );
		if ( thrusterDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			Spawn( select, thrusterDef.Prefab, placementTrans, false );
			ShootEffects( select );
		}
		else if ( Input.Pressed( "attack2" ) )
		{
			Spawn( select, thrusterDef.Prefab, placementTrans, true );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( thrusterDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );

	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile thrusterPrefab, Transform tx, bool noWeld )
	{
		if ( thrusterPrefab == null )
			return;

		var go = thrusterPrefab.GetScene().Clone();
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		if ( !noWeld && !point.GameObject.Tags.Contains( "world" ) )
		{
			var thruster = go.GetComponent<ThrusterEntity>();

			// attach it
			var joint = thruster.AddComponent<FixedJoint>();
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

		// undo
		{
			var undo = Player.Undo.Create();
			undo.Name = "Thruster";
			undo.Icon = "🚀";
			undo.Add( go );
		}

		CheckContraptionStats( point.GameObject );
	}

}
```

## Проверка
- Тулган в режиме Thruster показывает призрак двигателя на поверхности.
- ЛКМ размещает двигатель с приваркой к объекту.
- ПКМ размещает двигатель без приварки.
- Клавиша R переключает ось тяги.


---

# 🚀 Сущность двигателя (ThrusterEntity)

## Что мы делаем?
Создаём компонент сущности двигателя, который применяет силу тяги к физическому телу.

## Зачем это нужно?
`ThrusterEntity` — это логика самого двигателя: он принимает пользовательский ввод (активация/реверс) и прикладывает импульс к `Rigidbody`, заставляя объект двигаться.

## Как это работает внутри движка?
- Реализует `IPlayerControllable` для получения управления от игрока.
- `OnControl()` считывает аналоговые значения входов `Activate` и `Reverse`. Сама логика управления выполняется **только на хосте** (`Networking.IsHost`) — клиенты не пересчитывают физику.
- `AddThrust()` применяет импульс через `Rigidbody.ApplyImpulse()`.
- `SetActiveState()` помечен `[Rpc.Broadcast]` — изменение состояния включения и эффект `OnEffect` синхронизируются на всех клиентах одной RPC, без ручного `Network.Refresh()`.
- Управляет визуальными эффектами (`OnEffect`) и **зацикленным звуком двигателя** через `SoundDefinition` (см. [26.06 — Sound](26_06_Sound.md)).
- Звук стартует/останавливается в `OnUpdate()` — он опрашивает текущее `_state` и подгоняет состояние `_thrusterSound` под него. Раньше старт/стоп вызывались прямо из `SetActiveState`, но это терялось у клиентов; опрос-петля гарантирует, что звук тоже звучит на не-хост клиентах.
- Свойство `Power` регулирует силу тяги, `Invert` меняет направление.

## Создай файл
`Code/Weapons/ToolGun/Modes/Thruster/ThrusterEntity.cs`

```csharp
﻿[Alias( "thruster" )]
public class ThrusterEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public GameObject OnEffect { get; set; }

	[Property, ClientEditable, Range( 0, 1 )]
	public float Power { get; set; } = 0.5f;

	[Property, ClientEditable]
	public bool Invert { get; set; } = false;

	[Property, ClientEditable]
	public bool HideEffects { get; set; } = false;

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	/// <summary>
	/// While this input is active we'll apply thrust in the opposite direction
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Reverse { get; set; }

	private static SoundDefinition _defaultSound => ResourceLibrary.Get<SoundDefinition>( "entities/thruster/sounds/thruster_basic.sndef" );

	/// <summary>
	/// Looping sound played while the thruster is active.
	/// Filtered in the picker to only show <see cref="SoundDefinition"/>s with category "thruster".
	/// </summary>
	[Property, ClientEditable, Metadata( SoundDefinition.Thruster ), Group( "Sound" )]
	public SoundDefinition ThrusterSound { get; set; }

	/// <summary>
	/// Current thrust output, -1 to 1. Updated every control frame.
	/// </summary>
	public float ThrustAmount { get; private set; }

	private SoundHandle _thrusterSound;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		OnEffect?.Enabled = false;
	}

	protected override void OnDisabled()
	{
		_state = false;
		StopThrusterSound();
	}

	// Polled every frame on every client (host included): keeps the looping sound
	// in sync with the networked _state without RPC plumbing.
	protected override void OnUpdate()
	{
		if ( _state )
		{
			if ( !_thrusterSound.IsValid() )
				StartThrusterSound();
		}
		else
		{
			if ( _thrusterSound.IsValid() )
				StopThrusterSound();
		}
	}

	void AddThrust( float amount )
	{
		if ( amount.AlmostEqual( 0.0f ) ) return;

		var body = GetComponent<Rigidbody>();
		if ( body == null ) return;

		body.ApplyImpulse( WorldRotation.Up * -10000 * amount * Power * (Invert ? -1f : 1f) );
	}

	bool _state;

	[Rpc.Broadcast]
	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		if ( !HideEffects )
			OnEffect?.Enabled = state;
	}

	private void StartThrusterSound()
	{
		if ( _thrusterSound.IsValid() )
			StopThrusterSound();

		var sound = ThrusterSound ?? _defaultSound;
		if ( sound is null ) return;

		// SoundDefinition.Play( pos, parent ) wires up FollowParent for us.
		_thrusterSound = sound.Play( WorldPosition, GameObject );
	}

	private void StopThrusterSound()
	{
		if ( _thrusterSound.IsValid() )
		{
			_thrusterSound.Stop( 0.5f );
			_thrusterSound = default;
		}
	}

	public void OnControl()
	{
		// Only the host evaluates input → physics → state changes.
		// SetActiveState is broadcast, so clients still get the effect/state update.
		if ( !Networking.IsHost ) return;

		var forward = Activate.GetAnalog();
		var backward = Reverse.GetAnalog();
		var analog = forward - backward;
		ThrustAmount = analog;

		AddThrust( analog );

		var active = MathF.Abs( analog ) > 0.1f;

		if ( active != _state )
		{
			if ( active )
				Sandbox.Services.Stats.Increment( "tool.thruster.activate", 1 );

			SetActiveState( active );
		}
	}
}
```

## Проверка
- Двигатель активируется при нажатии назначенной клавиши.
- Объект получает импульс в направлении тяги.
- Визуальные эффекты и звук включаются/выключаются синхронно.
- Свойство `Invert` меняет направление тяги на противоположное.


---

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


---

## ➡️ Следующий шаг

Переходи к **[11.04 — 🛞 Инструмент Wheel (WheelTool)](11_04_Wheel.md)**.
