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
- `Toggle` включает/выключает левитацию (при выключении возвращается гравитация). При переключении проигрывается `EnableSound` или `DisableSound`.
- В `OnFixedUpdate()` вычисляет разницу между текущей и целевой высотой, корректирует `Velocity.z`.
- `AirResistance` добавляет горизонтальное торможение.
- `Speed` регулирует скорость изменения высоты.
- `IsEnabled` помечен `[Property, Sync, ClientEditable]` — клиент-владелец может менять состояние локально (с последующей синхронизацией хостом).
- `OnEffect` (визуальные частицы / свет «включен») синхронизируется с `IsEnabled` каждый кадр в `OnUpdate()`, поэтому состояние эффекта корректно и на прокси.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hoverball/HoverballEntity.cs`

```csharp
﻿[Alias( "hoverball" )]
public class HoverballEntity : Component, IPlayerControllable
{
	/// <summary>
	/// Is the hoverball on?
	/// </summary>
	[Property, Sync, ClientEditable]
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

	[Property] public SoundEvent EnableSound { get; set; }
	[Property] public SoundEvent DisableSound { get; set; }

	private float _zVelocity;
	private bool _toggleWasHeld;

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		TargetZ = WorldPosition.z;

		var rb = GetComponent<Rigidbody>();
		if ( rb.IsValid() )
			rb.Gravity = !IsEnabled;
	}

	protected override void OnUpdate()
	{
		if ( OnEffect.IsValid() )
			OnEffect.Enabled = IsEnabled;
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

		if ( IsEnabled )
			Sound.Play( EnableSound, WorldPosition );
		else
			Sound.Play( DisableSound, WorldPosition );

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
	}
}
```

## Проверка
- Ховербол удерживает объект на начальной высоте.
- Клавиши вверх/вниз изменяют целевую высоту.
- Переключатель включает/выключает левитацию.
- При переключении проигрываются `EnableSound` / `DisableSound`.
- Сопротивление воздуха замедляет горизонтальное движение.


---

# 🎱 Морфы и свечение ховербола (HoverballMorphs)

## Что мы делаем?
Создаём дополнительный компонент `HoverballMorphs`, который управляет морф-таргетами скелетной модели ховербола (`Coils_Deployed`, `Pins_Deployed`) и пульсацией свечения материала при включении.

## Зачем это нужно?
Морф-вариант префаба (`hoverball_morph.fbx`) — более производительная альтернатива ригу: вместо костей используется пара морфов, отвечающих за «раскрытые» катушки и шипы. `HoverballMorphs` плавно гонит эти морфы между 0 и 1 и подсвечивает материал, когда ховербол активен.

## Как это работает внутри движка?
- Берёт ссылку на `HoverballEntity` и `SkinnedModelRenderer` в дочернем объекте.
- Если задан `GlowMaterial`, создаёт его копию (`CreateCopy()`) и навешивает как `MaterialOverride`, отключая батчинг (`Batchable = false`), чтобы менять параметры материала индивидуально.
- В `OnUpdate()` вычисляет целевые значения морфов:
  - `Coils_Deployed` → `CoilDeployedValue` (по умолчанию 1), если `IsEnabled`, иначе 0.
  - `Pins_Deployed` → нормированное `AirResistance / PinRangeMax` (clamp 0..1) умноженное на `PinDeployedValue`.
- Анимация переходов идёт через `Easing.BounceOut`, длительность `TransitionDuration`.
- Свечение: `g_vSelfIllumTint` = `IllumTint` (по умолчанию бирюзовый) при включении, `Color.Black` иначе. Яркость `g_flSelfIllumBrightness` шумно мерцает между `IllumFlickerMin` и `IllumFlickerMax`, интервал между «вспышками» — случайный из диапазона `[IllumFlickerIntervalMin, IllumFlickerIntervalMax]`. Сглаживание выполняет `MathX.Approach` со скоростью `IllumFlickerSpeed`. Итоговое значение масштабируется на текущее значение `_coils`, чтобы плавно гасло вместе с морфом.

> **Изменено в апстриме:** все эти численные параметры (тинт, базовая яркость, диапазоны мерцания, диапазон пинов, целевые значения морфов) теперь выведены наружу как `[Property]` с группами `Illumination` / `Morphs`. Раньше в коде было захардкожено `(20,165,200)`, `8f`, `6f..8f`, `0.1f..0.4f`, `7f`, `5f`, `1f`, `1f` — теперь это редактируемые поля, и одна и та же `HoverballMorphs`-сборка может быть переиспользована для разных вариантов ховербола (например, базовый и премиум) с разной анимацией свечения. Морф-вариант префаба ховербола (`hoverball_morph.prefab`) опирается на эти значения по умолчанию.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hoverball/HoverballMorphs.cs`

```csharp
using Sandbox.Utility;

public sealed class HoverballMorphs : Component
{
	private HoverballEntity _hoverball;
	private SkinnedModelRenderer _renderer;
	private Material _glowMaterialCopy;

	private float _coils;
	private float _pins;
	private float _brightnessTarget;
	private float _brightnessCurrent;
	private float _brightnessTimer;

	private float _coilsFrom;
	private float _coilsTo;
	private float _coilsTime;
	private float _pinsFrom;
	private float _pinsTo;
	private float _pinsTime;

	[Property] public float Speed { get; set; } = 15f;
	[Property] public float TransitionDuration { get; set; } = 0.5f;
	[Property] public Material GlowMaterial { get; set; }

	[Property, Group( "Illumination" )] public Color IllumTint { get; set; } = Color.FromBytes( 20, 165, 200 );
	[Property, Group( "Illumination" )] public float IllumBrightness { get; set; } = 8f;
	[Property, Group( "Illumination" ), Title( "Flicker Min" )] public float IllumFlickerMin { get; set; } = 6f;
	[Property, Group( "Illumination" ), Title( "Flicker Max" )] public float IllumFlickerMax { get; set; } = 8f;
	[Property, Group( "Illumination" ), Title( "Flicker Interval Min" )] public float IllumFlickerIntervalMin { get; set; } = 0.1f;
	[Property, Group( "Illumination" ), Title( "Flicker Interval Max" )] public float IllumFlickerIntervalMax { get; set; } = 0.4f;
	[Property, Group( "Illumination" ), Title( "Flicker Approach Speed" )] public float IllumFlickerSpeed { get; set; } = 7f;

	[Property, Group( "Morphs" ), Title( "Pin Range Max" )] public float PinRangeMax { get; set; } = 5f;
	[Property, Group( "Morphs" ), Title( "Pin Deployed" )] public float PinDeployedValue { get; set; } = 1f;
	[Property, Group( "Morphs" ), Title( "Coil Deployed" )] public float CoilDeployedValue { get; set; } = 1f;

	protected override void OnStart()
	{
		_hoverball = GetComponent<HoverballEntity>();
		_renderer = GetComponentInChildren<SkinnedModelRenderer>();

		if ( GlowMaterial is not null && _renderer.IsValid() )
		{
			_glowMaterialCopy = GlowMaterial.CreateCopy();
			_renderer.MaterialOverride = _glowMaterialCopy;
			_renderer.SceneModel.Batchable = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( !_hoverball.IsValid() || !_renderer.IsValid() ) return;

		var targetCoils = _hoverball.IsEnabled ? CoilDeployedValue : 0f;
		var targetPins = Math.Clamp( _hoverball.AirResistance / PinRangeMax, 0f, 1f ) * PinDeployedValue;

		if ( targetCoils != _coilsTo )
		{
			_coilsFrom = _coils;
			_coilsTo = targetCoils;
			_coilsTime = 0f;
		}

		if ( targetPins != _pinsTo )
		{
			_pinsFrom = _pins;
			_pinsTo = targetPins;
			_pinsTime = 0f;
		}

		_coilsTime = Math.Min( _coilsTime + Time.Delta / TransitionDuration, 1f );
		_pinsTime = Math.Min( _pinsTime + Time.Delta / TransitionDuration, 1f );

		_coils = MathX.Lerp( _coilsFrom, _coilsTo, Easing.BounceOut( _coilsTime ) );
		_pins = MathX.Lerp( _pinsFrom, _pinsTo, Easing.BounceOut( _pinsTime ) );

		_renderer.SceneModel?.Morphs.Set( "Coils_Deployed", _coils );
		_renderer.SceneModel?.Morphs.Set( "Pins_Deployed", _pins );

		UpdateGlowMaterial();
	}

	void UpdateGlowMaterial()
	{
		if ( _glowMaterialCopy is null ) return;

		var brightness = _hoverball.IsEnabled ? IllumBrightness : 0f;

		if ( _hoverball.IsEnabled )
		{
			_brightnessTimer -= Time.Delta;
			if ( _brightnessTimer <= 0f )
			{
				_brightnessTarget = Random.Shared.Float( IllumFlickerMin, IllumFlickerMax );
				_brightnessTimer = Random.Shared.Float( IllumFlickerIntervalMin, IllumFlickerIntervalMax );
			}
			_brightnessCurrent = MathX.Approach( _brightnessCurrent, _brightnessTarget, Time.Delta * IllumFlickerSpeed );
			brightness = _brightnessCurrent;
		}

		_glowMaterialCopy.Set( "g_vSelfIllumTint", _hoverball.IsEnabled ? IllumTint : Color.Black );
		_glowMaterialCopy.Set( "g_flSelfIllumBrightness", brightness * _coils );
	}
}
```

## Проверка
- В морф-варианте префаба ховербола (`hoverball_morph.prefab`) при включении плавно «раскрываются» катушки и шипы.
- Материал начинает мерцать бирюзовым self-illum, гаснет при выключении.
- Уровень `AirResistance` тула виден на модели — чем выше, тем больше выдвигаются «шипы».


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
