# Этап 11_07 — Emitter (Эмиттер — источник частиц)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что мы делаем?

Создаём систему эмиттеров: инструмент `EmitterTool` для размещения источников частиц, ресурс `ScriptedEmitter` для описания эффектов и компонент `EmitterEntity` для управления частицами в мире. Игрок размещает эмиттер на поверхность, и тот начинает испускать выбранный эффект (искры, дым, огонь и т.д.).

## Зачем это нужно?

Представьте фабрику спецэффектов: вы ставите «коробочку» на стену или пол, и из неё начинают лететь искры, идти дым или бить фонтан. Эмиттер — это и есть такая коробочка:

- **Размещение** — ставится инструментом Tool Gun на любую поверхность
- **Настройка эффекта** — можно выбрать тип частиц (искры, дым и т.д.)
- **Управление** — включается/выключается по нажатию клавиши (Toggle) или удержанию (Hold)
- **Привязка к объектам** — может быть прикреплён к движущемуся объекту через FixedJoint

## Как это работает внутри движка?

### Архитектура (три файла)

```
EmitterTool (ToolMode)          — инструмент: выбор и размещение
  ├── BaseDef (.smemit)         — ресурс модели эмиттера (физическое тело)
  └── EffectDef (.semit)        — ресурс эффекта (какие частицы испускать)

ScriptedEmitter (GameResource)  — описание эффекта: хранит Prefab с частицами
  └── .semit файл

EmitterEntity (Component)       — компонент на объекте в мире: управляет включением/выключением
  ├── Emitter → ScriptedEmitter — какой эффект использовать
  ├── Mode (Toggle/Hold)        — режим управления
  └── IsEmitting (Sync)         — текущее состояние (вкл/выкл)
```

### Процесс размещения

1. Игрок выбирает `EmitterTool` в Toolgun
2. Выбирает базовую модель (`BaseDef`) и эффект (`EffectDef`)
3. Наводит на поверхность — показывается превью
4. Нажимает ЛКМ → на сервере создаётся объект-эмиттер
5. Если поверхность — другой объект (не мир), создаётся `FixedJoint` для привязки

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Emitter/EmitterTool.cs`

```csharp
using Sandbox.UI;

[Hide]
[Title( "Emitter" )]
[Icon( "💨" )]
[ClassName( "emittertool" )]
[Group( "Building" )]
public class EmitterTool : ToolMode
{
	public override bool UseSnapGrid => true;
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	/// <summary>
	/// The physical emitter body to spawn (model + physics).
	/// </summary>
	[Property, ResourceSelect( Extension = "smemit", AllowPackages = true ), Title( "Base" )]
	public string BaseDef { get; set; } = "entities/emitter/basic.smemit";

	/// <summary>
	/// The particle/VFX effect the emitter will produce.
	/// </summary>
	[Property, ResourceSelect( Extension = "semit", AllowPackages = true ), Title( "Effect" )]
	public string EffectDef { get; set; } = "entities/particles/sparks.semit";

	public override string Description => "#tool.hint.emittertool.description";
	public override string PrimaryAction => "#tool.hint.emittertool.place";

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );
		placementTrans.Rotation = pos.Rotation;

		var baseDef = ResourceLibrary.Get<ScriptedEmitterModel>( BaseDef );
		if ( baseDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			var effectDef = ResourceLibrary.Get<ScriptedEmitter>( EffectDef );
			Spawn( select, baseDef.Prefab, effectDef, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( baseDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile emitterPrefab, ScriptedEmitter effect, Transform tx )
	{
		if ( emitterPrefab == null )
			return;

		var go = emitterPrefab.GetScene().Clone();
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		var emitter = go.GetComponent<EmitterEntity>( true );
		if ( emitter.IsValid() && effect != null )
		{
			emitter.Emitter = effect;
		}

		if ( !point.IsWorld )
		{
			var joint = go.AddComponent<FixedJoint>();
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
		undo.Name = "Emitter";
		undo.Icon = "💨";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}
}
```

### Разбор EmitterTool

- **`[Hide]`** — инструмент скрыт из стандартного меню (доступен программно или через особые условия).
- **`UseSnapGrid => true`** — включает сетку привязки для точного размещения.
- **`TraceIgnoreTags`** — трассировка луча игнорирует объекты с тегами `constraint` и `collision`.
- **`BaseDef`** / **`EffectDef`** — пути к ресурсам. `ResourceSelect` с фильтром по расширению файла. `AllowPackages = true` позволяет использовать ресурсы из пакетов Workshop.
- **`OnControl()`** — каждый кадр:
  1. Трассирует луч (`TraceSelect()`) для определения точки размещения.
  2. Загружает ресурс модели (`ScriptedEmitterModel`).
  3. При нажатии ЛКМ — вызывает `Spawn()` на сервере.
  4. Рисует полупрозрачное превью модели через `DebugOverlay.GameObject()`.
- **`Spawn()` (`[Rpc.Host]`)** — выполняется на сервере:
  1. Клонирует префаб модели эмиттера.
  2. Назначает тег `removable` (можно удалить Remover-ом) и `constraint`.
  3. Устанавливает позицию и поворот.
  4. Назначает эффект (`EmitterEntity.Emitter`).
  5. Если размещён на объекте (не на мире) — создаёт `FixedJoint` для привязки.
  6. Регистрирует undo и спавнит в сети.

---

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

### Разбор ScriptedEmitter

- **`GameResource`** — базовый класс для пользовательских ресурсов (ассетов) в s&box. Файлы `.semit` — это ресурсы эмиттеров.
- **`[AssetType]`** — регистрирует тип ассета в редакторе: имя, расширение, категория. `NoEmbedding` — нельзя встраивать в другие ресурсы, `IncludeThumbnails` — генерирует превью.
- **`IDefinitionResource`** — маркерный интерфейс, обозначающий ресурс как определение (definition).
- **`Prefab`** — главное свойство: указывает на префаб с системой частиц.
- **`RenderThumbnail()`** — создаёт превью для редактора: рендерит префаб в битмап.
- **`ConfigurePublishing()`** — валидация перед публикацией в Workshop: запрещает публикацию если нет префаба, или если в префабе есть `ModelRenderer` или `BaseCarryable` (эмиттер должен содержать только частицы).

---

📁 `Code/Game/Entity/EmitterEntity.cs`

```csharp
/// <summary>
/// Whether the emitter fires while the input is held, or toggles on/off with a press.
/// </summary>
public enum EmitMode
{
	/// <summary>
	/// Press once to turn on, press again to turn off.
	/// </summary>
	Toggle,
	/// <summary>
	/// Emits only while the input is held down.
	/// </summary>
	Hold,
}

/// <summary>
/// A world-placed SENT that spawns and controls a particle/VFX emitter.
/// The emitter prefab is defined by a <see cref="ScriptedEmitter"/> resource.
/// </summary>
[Alias( "emitter" )]
public class EmitterEntity : Component, IPlayerControllable
{
	/// <summary>
	/// The emitter definition points to a prefab containing a particle system.
	/// </summary>
	[Property, ClientEditable]
	public ScriptedEmitter Emitter { get; set; }

	/// <summary>
	/// Whether this emitter toggles on/off with a press, or emits only while held.
	/// </summary>
	[Property, ClientEditable]
	public EmitMode Mode { get; set; } = EmitMode.Toggle;

	/// <summary>
	/// Used when <see cref="Mode"/> is <see cref="EmitMode.Toggle"/>.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Input" )]
	public ClientInput ToggleInput { get; set; }

	/// <summary>
	/// Used when <see cref="Mode"/> is <see cref="EmitMode.Hold"/>.
	/// </summary>
	[Property, Sync, ClientEditable, Group( "Input" )]
	public ClientInput HoldInput { get; set; }

	/// <summary>
	/// Whether the emitter is currently active. Synced to all clients.
	/// </summary>
	[Sync] public bool IsEmitting { get; private set; }

	/// <summary>
	/// When enabled, forces the emitter on regardless of input or mode.
	/// Can be set from the editor or wired up externally.
	/// </summary>
	[Property, ClientEditable]
	public bool ManualOn
	{
		get => _manualOn;
		set { _manualOn = value; if ( !IsProxy ) UpdateEmitState(); }
	}
	private bool _manualOn;
	private bool _inputEmitting;

	private GameObject _particleInstance;
	private ScriptedEmitter _lastEmitter;

	protected override void OnStart() { }

	protected override void OnUpdate()
	{
		// Emitter resource changed — destroy existing instance so it gets recreated
		if ( _lastEmitter != Emitter && _particleInstance.IsValid() )
			DestroyParticle();

		_lastEmitter = Emitter;

		if ( IsEmitting && !_particleInstance.IsValid() )
			SpawnParticle();
		else if ( !IsEmitting && _particleInstance.IsValid() )
			DestroyParticle();
	}

	void IPlayerControllable.OnStartControl() { }
	void IPlayerControllable.OnEndControl()
	{
		if ( Mode == EmitMode.Hold )
		{
			_inputEmitting = false;
			UpdateEmitState();
		}
	}

	void IPlayerControllable.OnControl()
	{
		if ( Mode == EmitMode.Toggle )
		{
			if ( ToggleInput.Pressed() )
			{
				_inputEmitting = !_inputEmitting;
				UpdateEmitState();
			}
		}
		else
		{
			var held = HoldInput.Down();
			if ( held != _inputEmitting )
			{
				_inputEmitting = held;
				UpdateEmitState();
			}
		}
	}

	private void UpdateEmitState() => SetEmitting( _inputEmitting || _manualOn );

	[Rpc.Broadcast]
	private void SetEmitting( bool active )
	{
		IsEmitting = active;
	}

	private void SpawnParticle()
	{
		if ( !Emitter.IsValid() || Emitter.Prefab is null ) return;

		_particleInstance = GameObject.Clone( Emitter.Prefab, new CloneConfig
		{
			Parent = GameObject,
			Transform = new Transform( Vector3.Forward * 4f ),
			StartEnabled = true,
		} );
	}

	private void DestroyParticle()
	{
		_particleInstance.Destroy();
		_particleInstance = null;
	}
}
```

### Разбор EmitterEntity

- **`EmitMode`** — перечисление с двумя режимами: `Toggle` (нажми чтобы включить/выключить) и `Hold` (держи чтобы работало).

- **`IPlayerControllable`** — интерфейс для управления игроком (через систему управления сущностями). Методы `OnStartControl`, `OnEndControl`, `OnControl` вызываются когда игрок берёт/отпускает управление.

- **`[Sync]`** — атрибут синхронизации: `IsEmitting` синхронизируется между сервером и всеми клиентами.

- **`[ClientEditable]`** — свойства, которые игрок может менять через контекстное меню (ПКМ по объекту).

- **`ManualOn`** — принудительное включение. Сеттер сразу обновляет состояние (`UpdateEmitState`), но только если это не прокси-объект (`!IsProxy`).

- **`OnUpdate()`** — каждый кадр проверяет:
  1. Если ресурс эмиттера изменился — уничтожает старую частицу.
  2. Если `IsEmitting == true` и нет частицы — создаёт.
  3. Если `IsEmitting == false` и частица есть — уничтожает.

- **`UpdateEmitState()`** — объединяет ввод игрока и `ManualOn`: эмиттер работает если хотя бы одно из условий истинно.

- **`SetEmitting()` (`[Rpc.Broadcast]`)** — RPC, вызываемый на всех клиентах: устанавливает `IsEmitting` синхронно.

- **`SpawnParticle()`** — клонирует префаб частиц как дочерний объект эмиттера, со смещением `Vector3.Forward * 4f` (4 единицы вперёд).

- **`DestroyParticle()`** — уничтожает экземпляр частиц.


---


---

<!-- seealso -->
## 🔗 См. также

- [06.01 — BaseCarryable](06_01_BaseCarryable.md)
- [09.06 — Toolgun](09_06_Toolgun.md)

## ➡️ Следующий шаг

Переходи к **[11.08 — Этап 11_08 — Duplicator (Дупликатор — копирование конструкций)](11_08_Duplicator.md)**.
