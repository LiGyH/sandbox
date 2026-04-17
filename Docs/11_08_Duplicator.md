# Этап 11_08 — Duplicator (Дупликатор — копирование конструкций)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

<!-- toc -->
> **📑 Оглавление** (файл большой, используй ссылки для быстрой навигации)
>
> - [Что мы делаем?](#что-мы-делаем)
> - [Зачем это нужно?](#зачем-это-нужно)
> - [Как это работает внутри движка?](#как-это-работает-внутри-движка)
> - [Создай файл](#создай-файл)

## Что мы делаем?

Создаём систему дупликатора: инструмент `Duplicator` для копирования и вставки конструкций, класс `DuplicationData` для сериализации данных, `LinkedGameObjectBuilder` для поиска связанных объектов и `Duplicator.IconRendering` для рендеринга превью. ПКМ — скопировать конструкцию, ЛКМ — вставить копию.

## Зачем это нужно?

Представьте 3D-принтер в песочнице: вы «сканируете» построенную конструкцию (машину, дом, механизм), она сохраняется как чертёж, и вы можете «печатать» копии в любом месте. Дупликатор:

- **Копирует** конструкции целиком — все связанные объекты, соединения, настройки
- **Вставляет** копии с превью-отображением (полупрозрачная «голограмма»)
- **Вращает** превью перед размещением (через Use + мышь)
- **Сохраняет** конструкции в локальное хранилище и Workshop
- **Загружает** сохранённые конструкции из хранилища или Workshop
- **Работает в сети** — другие игроки видят ваше превью размещения

## Как это работает внутри движка?

### Архитектура (четыре файла)

```
Duplicator (ToolMode, partial class)
  ├── Duplicator.cs              — основная логика: копирование, вставка, ввод
  └── Duplicator.IconRendering.cs — рендеринг превью в битмап (для сохранений)

DuplicationData                  — данные копии: JSON объектов, bounds, превью-модели
LinkedGameObjectBuilder          — поиск всех связанных объектов (по Joint и ManualLink)
```

### Процесс копирования

1. Игрок наводит на объект и нажимает ПКМ
2. `LinkedGameObjectBuilder.AddConnected()` находит все связанные объекты (через Joints, Colliders, ManualLinks)
3. `DuplicationData.CreateFromObjects()` сериализует объекты в JSON с относительными координатами
4. JSON сохраняется в `CopiedJson` (синхронизируется по сети)
5. Из JSON создаётся `DuplicatorSpawner` с превью-моделями

### Процесс вставки

1. Игрок видит полупрозрачную «голограмму» конструкции
2. Может вращать её через Use + мышь (с привязкой к 45° при удержании Shift/Run)
3. Нажимает ЛКМ → `Duplicate()` на сервере
4. `DuplicatorSpawner.Spawn()` десериализует объекты и размещает в мире

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Duplicator/Duplicator.cs`

```csharp
﻿﻿using Sandbox.UI;
using System.Text.Json;
using System.Text.Json.Nodes;

[Icon( "✌️" )]
[ClassName( "duplicator" )]
[Group( "Building" )]
public partial class Duplicator : ToolMode
{
	/// <summary>
	/// When we right click, to "copy" something, we create a Duplication object
	/// and serialize it to Json and store it here.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( JsonChanged ) )]
	public string CopiedJson { get; set; }

	DuplicatorSpawner spawner;
	LinkedGameObjectBuilder builder = new() { RejectPlayers = true };

	Rotation _rotationOffset = Rotation.Identity;
	Rotation _spinRotation = Rotation.Identity;
	Rotation _snapRotation = Rotation.Identity;
	bool _isSnapping;
	bool _isRotating;

	public override string Description => "#tool.hint.duplicator.description";
	public override string PrimaryAction => spawner is not null ? "#tool.hint.duplicator.place" : null;
	public override string SecondaryAction => "#tool.hint.duplicator.copy";

	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );

		if ( _isRotating )
			angles = default;
	}

	public override void OnControl()
	{
		base.OnControl();

		_isRotating = spawner is not null && Input.Down( "use" );
		Toolgun.SetIsUsingJoystick( _isRotating );

		var isSnapping = Input.Down( "run" );
		if ( !isSnapping && _isSnapping ) _spinRotation = _snapRotation;
		_isSnapping = isSnapping;

		if ( _isRotating )
		{
			var look = Input.AnalogLook with { pitch = 0 };

			if ( _isSnapping )
			{
				if ( MathF.Abs( look.yaw ) > MathF.Abs( look.pitch ) ) look.pitch = 0;
				else look.yaw = 0;
			}

			_spinRotation = Rotation.From( look ) * _spinRotation;
			Input.Clear( "use" );

			if ( _isSnapping )
			{
				var snapped = _spinRotation.Angles();
				_rotationOffset = snapped.SnapToGrid( 45f );
			}
			else
			{
				_rotationOffset = _spinRotation;
			}

			_snapRotation = _rotationOffset;

			Toolgun.UpdateJoystick( new Angles( look.yaw, look.pitch, 0 ) );
		}

		var select = TraceSelect();
		IsValidState = IsValidTarget( select );

		if ( spawner is { IsReady: true } && Input.Pressed( "attack1" ) )
		{
			if ( !IsValidPlacementTarget( select ) )
			{
				// make invalid noise
				return;
			}

			var tx = new Transform();
			tx.Position = select.WorldPosition() + Vector3.Down * spawner.Bounds.Mins.z;

			var relative = Player.EyeTransform.Rotation.Angles();
			tx.Rotation = Rotation.From( new Angles( 0, relative.yaw, 0 ) ) * _rotationOffset;

			Duplicate( tx );
			ShootEffects( select );
			_rotationOffset = Rotation.Identity;
			_spinRotation = Rotation.Identity;
			return;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			if ( !IsValidState )
			{
				CopiedJson = default;
				return;
			}

			var selectionAngle = new Transform( select.WorldPosition(), Player.EyeTransform.Rotation.Angles().WithPitch( 0 ) );
			Copy( select.GameObject, selectionAngle, Input.Down( "run" ) );

			ShootEffects( select );
		}
	}

	/// <summary>
	/// Save the current dupe to storage.
	/// </summary>
	public void Save()
	{
		string data = CopiedJson;
		var packages = Cloud.ResolvePrimaryAssetsFromJson( data );

		var storage = Storage.CreateEntry( "dupe" );
		storage.SetMeta( "packages", packages.Select( x => x.FullIdent ) );
		storage.Files.WriteAllText( "/dupe.json", data );

		var bitmap = new Bitmap( 1024, 1024 );
		RenderIconToBitmap( data, bitmap );

		var downscaled = bitmap.Resize( 512, 512 );
		storage.SetThumbnail( downscaled );
	}

	[Rpc.Host]
	public void Load( string json )
	{
		CopiedJson = json;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// this is called on every client, so we can see what the other
		// players are placing. It's kind of cool.
		DrawPreview();
	}

	[Rpc.Host]
	public void Copy( GameObject obj, Transform selectionAngle, bool additive )
	{
		if ( !additive )
			builder.Clear();

		builder.AddConnected( obj );
		builder.RemoveDeletedObjects();

		var tempDupe = DuplicationData.CreateFromObjects( builder.Objects, selectionAngle );

		CopiedJson = Json.Serialize( tempDupe );

		PlayerData.For( Rpc.Caller )?.AddStat( "tool.duplicator.copy" );
	}

	void JsonChanged()
	{
		spawner = null;

		if ( string.IsNullOrWhiteSpace( CopiedJson ) )
			return;

		spawner = DuplicatorSpawner.FromJson( CopiedJson );
	}

	void DrawPreview()
	{
		if ( spawner is null ) return;

		var select = TraceSelect();
		if ( !IsValidPlacementTarget( select ) ) return;

		var tx = new Transform();

		tx.Position = select.WorldPosition() + Vector3.Down * spawner.Bounds.Mins.z;

		var relative = Player.EyeTransform.Rotation.Angles();
		tx.Rotation = Rotation.From( new Angles( 0, relative.yaw, 0 ) ) * _rotationOffset;

		var overlayMaterial = IsProxy ? Material.Load( "materials/effects/duplicator_override_other.vmat" ) : Material.Load( "materials/effects/duplicator_override.vmat" );
		spawner.DrawPreview( tx, overlayMaterial );
	}


	bool IsValidTarget( SelectionPoint source )
	{
		if ( !source.IsValid() ) return false;
		if ( source.IsWorld ) return false;
		if ( source.IsPlayer ) return false;

		return true;
	}

	bool IsValidPlacementTarget( SelectionPoint source )
	{
		if ( !source.IsValid() ) return false;

		return true;
	}

	[Rpc.Host]
	public async void Duplicate( Transform dest )
	{
		if ( spawner is null )
			return;

		var player = Player.FindForConnection( Rpc.Caller );
		if ( player is null ) return;

		var objects = await spawner.Spawn( dest, player );

		if ( objects is { Count: > 0 } )
		{
			var undo = player.Undo.Create();
			undo.Name = "Duplication";

			foreach ( var go in objects )
			{
				undo.Add( go );
			}

			player.PlayerData?.AddStat( "tool.duplicator.spawn" );
		}
	}

	public static void FromStorage( Storage.Entry item )
	{
		var localPlayer = Player.FindLocalPlayer();
		if ( localPlayer == null ) return;

		var inventory = localPlayer.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		inventory.SetToolMode( "Duplicator" );

		var toolmode = localPlayer.GetComponentInChildren<Duplicator>( true );

		// we don't have a duplicator tool!
		if ( toolmode is null ) return;

		var json = item.Files.ReadAllText( "/dupe.json" );
		toolmode.Load( json );
	}

	public static async Task FromWorkshop( Storage.QueryItem item )
	{
		var notice = Notices.AddNotice( "downloading", Color.Yellow, $"Installing {item.Title}..", 0 );
		notice?.AddClass( "progress" );

		var installed = await item.Install();

		notice?.Dismiss();

		if ( installed == null ) return;

		FromStorage( installed );
	}
}
```

### Разбор ключевых частей Duplicator.cs

- **`partial class`** — класс разделён на два файла: основная логика и рендеринг иконок.
- **`[Sync(SyncFlags.FromHost), Change(nameof(JsonChanged))]`** — `CopiedJson` синхронизируется от хоста ко всем клиентам. При изменении вызывается `JsonChanged()`, создающий `DuplicatorSpawner` из JSON.
- **`builder = new() { RejectPlayers = true }`** — сборщик связанных объектов, который игнорирует игроков (чтобы не скопировать самого себя).
- **Вращение превью** — три состояния поворота: `_spinRotation` (свободное), `_snapRotation` (привязка к сетке), `_rotationOffset` (итоговый). При удержании `run` (Shift) — привязка к 45°.
- **`OnCameraMove()`** — при вращении (`_isRotating`) обнуляет движение камеры, чтобы мышь управляла только поворотом объекта.
- **`Copy()` (`[Rpc.Host]`)** — выполняется на сервере: добавляет связанные объекты, сериализует в JSON. Параметр `additive` позволяет добавлять объекты к существующей копии (при удержании Shift).
- **`Duplicate()` (`[Rpc.Host]`)** — асинхронный спавн: ожидает загрузки пакетов, создаёт объекты, регистрирует undo.
- **`Save()`** — сохраняет в локальное хранилище: JSON + превью-изображение 512×512.
- **`FromStorage()` / `FromWorkshop()`** — статические методы загрузки: переключают инструмент на дупликатор и загружают JSON.
- **`DrawPreview()`** — вызывается каждый кадр на всех клиентах (`OnUpdate`). Для своего превью используется один материал, для чужого (`IsProxy`) — другой.

---

📁 `Code/Weapons/ToolGun/Modes/Duplicator/Duplicator.IconRendering.cs`

```csharp
﻿using System.Text.Json;
using System.Text.Json.Nodes;

public partial class Duplicator
{
	/// <summary>
	/// Render duplicator Json to a bitmap
	/// </summary>
	public static void RenderIconToBitmap( string duplicatorJson, Bitmap bitmap )
	{
		var jsonObject = Json.ParseToJsonObject( duplicatorJson );
		bitmap.Clear( Color.Transparent );

		Transform dest = new();

		var scene = Scene.CreateEditorScene();
		using ( scene.Push() )
		{
			var root = new GameObject();
			foreach ( var entry in jsonObject["Objects"] as JsonArray )
			{
				if ( entry is JsonObject obj )
				{
					var pos = entry["Position"]?.Deserialize<Vector3>() ?? default;
					var rot = entry["Rotation"]?.Deserialize<Rotation>() ?? Rotation.Identity;

					var world = dest.ToWorld( new Transform( pos, rot ) );

					var go = new GameObject( false );
					go.Deserialize( obj, new GameObject.DeserializeOptions { TransformOverride = world } );
					go.NetworkSpawn( true, null );

					go.Parent = root;
				}
			}

			SceneUtility.RenderGameObjectToBitmap( root, bitmap );
			scene.Destroy();
		}
	}

}
```

### Разбор IconRendering

- **`partial class Duplicator`** — продолжение основного класса.
- **`RenderIconToBitmap()`** — статический метод для создания превью конструкции:
  1. Парсит JSON дупликации.
  2. Создаёт временную редакторскую сцену (`Scene.CreateEditorScene()`).
  3. Для каждого объекта в JSON: десериализует позицию и поворот, создаёт `GameObject`, десериализует его из JSON.
  4. Все объекты помещаются под общий корневой `root`.
  5. `SceneUtility.RenderGameObjectToBitmap()` рендерит всю конструкцию в битмап.
  6. Сцена уничтожается после использования.

---

📁 `Code/Weapons/ToolGun/Modes/Duplicator/DuplicationData.cs`

```csharp
﻿using System.Text.Json.Nodes;

/// <summary>
/// Holds a bunch of GameObject json, a bounding box, and some preview models for a
/// duplication. This is what gets serialized to a string and stored in the Duplicator tool.
/// The objects and the bounds are created in selection space. Where the user right clicked to 
/// select is 0,0,0, and the player's view yaw is the rotation identity.
/// </summary>
public class DuplicationData
{
	/// <summary>
	/// An array of JsonObject objects, which are serialzed GameObjects
	/// </summary>
	public JsonArray Objects { get; set; }

	/// <summary>
	/// The bounds are used to work out where to place the duplication, so it
	/// doesn't clip through the floor.
	/// </summary>
	public BBox Bounds { get; set; }

	/// <summary>
	/// Describes where to draw a model for the preview
	/// </summary>
	public record struct PreviewModel( Model Model, Transform Transform, Transform[] Bones, BBox Bounds );

	/// <summary>
	/// A list of preview models to help visualze where the duplication will be placed
	/// </summary>
	public List<PreviewModel> PreviewModels { get; set; }

	/// <summary>
	/// Packages used in this
	/// </summary>
	public List<string> Packages { get; set; }

	/// <summary>
	/// Create DuplicationData from a bunch of objects.
	/// center is the transform to use as the origin for the duplication.
	/// The rotation of center should be the player's view yaw when they made the selection.
	/// </summary>
	public static DuplicationData CreateFromObjects( IEnumerable<GameObject> objects, Transform center )
	{
		var dupe = new DuplicationData();
		dupe.Objects = new JsonArray();
		dupe.Bounds = BBox.FromPositionAndSize( 0, 0.01f );
		dupe.PreviewModels = new();

		List<BBox> worldBounds = new List<BBox>();

		foreach ( var obj in objects )
		{
			var entry = obj.Serialize();
			worldBounds.Add( GetWorldBounds( obj ) );

			var localized = center.ToLocal( obj.WorldTransform );
			entry["Position"] = JsonValue.Create( localized.Position );
			entry["Rotation"] = JsonValue.Create( localized.Rotation );
			entry["Scale"] = JsonValue.Create( localized.Scale );

			dupe.Objects.Add( entry );

			foreach ( var renderer in obj.GetComponentsInChildren<ModelRenderer>() )
			{
				var model = renderer.Model ?? Model.Cube;

				if ( model.IsError ) continue;

				Transform[] bones = null;

				if ( renderer is SkinnedModelRenderer skinned )
				{
					bones = skinned.GetBoneTransforms( false );
				}

				var modelTx = center.ToLocal( renderer.WorldTransform );
				dupe.PreviewModels.Add( new DuplicationData.PreviewModel( model, modelTx, bones, model.Bounds ) );
			}
		}

		if ( worldBounds.Count > 0 )
		{
			var txi = new Transform( -center.Position, center.Rotation.Inverse );

			dupe.Bounds = BBox.FromBoxes( worldBounds.Select( x => x.Transform( txi ) ) );
		}

		var packages = Cloud.ResolvePrimaryAssetsFromJson( dupe.Objects );
		dupe.Packages = packages.Select( x => x.FullIdent ).ToList();


		return dupe;
	}

	public static BBox GetWorldBounds( GameObject go )
	{
		BBox box = BBox.FromPositionAndSize( 0, 0.01f );

		var rb = go.GetComponentsInChildren<Collider>( false, true ).ToArray();
		if ( rb.Length > 0 )
		{
			box = rb[0].GetWorldBounds();

			foreach ( var b in rb )
			{
				box = box.AddBBox( b.GetWorldBounds() );
			}
		}

		return box;
	}
}
```

### Разбор DuplicationData

- **Пространство выделения (Selection Space)** — все координаты хранятся относительно точки, где игрок нажал ПКМ. Позиция 0,0,0 = место клика, поворот Identity = направление взгляда игрока (yaw). Это позволяет размещать копию в любом месте и с любым поворотом.

- **`Objects`** — массив сериализованных `GameObject` в формате JSON. Каждый объект содержит все компоненты, свойства и дочерние элементы.

- **`Bounds`** — ограничивающий объём (bounding box) всей конструкции. Используется для корректного размещения: `Vector3.Down * spawner.Bounds.Mins.z` поднимает конструкцию, чтобы она не проваливалась в пол.

- **`PreviewModel`** — record struct для превью: модель, трансформация, кости (для скинированных моделей) и bounds. Используются для отрисовки голограммы.

- **`CreateFromObjects(objects, center)`** — основной метод:
  1. Для каждого объекта: сериализует в JSON, пересчитывает координаты в локальное пространство (`center.ToLocal()`).
  2. Собирает модели для превью (включая кости скинированных моделей).
  3. Вычисляет общий bounding box.
  4. Разрешает зависимости пакетов (`Cloud.ResolvePrimaryAssetsFromJson`).

- **`GetWorldBounds()`** — вычисляет bounds объекта по всем его коллайдерам.

---

📁 `Code/Weapons/ToolGun/Modes/Duplicator/LinkedGameObjectBuilder.cs`

```csharp
﻿

public class LinkedGameObjectBuilder
{
	public List<GameObject> Objects { get; } = new();

	/// <summary>
	/// Reject players and any objects with players as descendants. This is used for the duplicator to avoid accidentally duping the player and all their attachments.
	/// </summary>
	public bool RejectPlayers { get; set; } = false;

	/// <summary>
	/// Adds a GameObject. Won't find connections.
	/// </summary>
	public bool Add( GameObject obj )
	{
		if ( !obj.IsValid() ) return false;
		if ( obj.Tags.Contains( "world" ) ) return false;
		if ( Objects.Contains( obj ) ) return false;
		if ( obj.GetComponent<MapInstance>() is not null ) return false;
		if ( RejectPlayers && HasDescendantWithTag( obj, "player" ) ) return false;

		Objects.Add( obj );
		return true;
	}

	/// <summary>
	/// Add a GameObject with all connected GameObjects
	/// </summary>
	public void AddConnected( GameObject source )
	{
		if ( !source.IsValid() ) return;

		//
		// we're only interested in root objects
		//
		source = source.Root;

		// If we can't add this then don't add children
		// because we must have already added it, or it's the world.
		if ( !Add( source ) ) return;

		foreach ( var rb in source.GetComponentsInChildren<Rigidbody>() )
		{
			foreach ( var joint in rb.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		foreach ( var collider in source.GetComponentsInChildren<Collider>() )
		{
			foreach ( var joint in collider.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		foreach ( var link in source.GetComponentsInChildren<ManualLink>() )
		{
			if ( link.Body.IsValid() )
				AddConnected( link.Body );
		}
	}

	public void RemoveDeletedObjects()
	{
		Objects.RemoveAll( x => !x.IsValid() || x.IsDestroyed );
	}

	static bool HasDescendantWithTag( GameObject obj, string tag )
	{
		foreach ( var child in obj.Children )
		{
			if ( child.Tags.Has( tag ) ) return true;
			if ( HasDescendantWithTag( child, tag ) ) return true;
		}
		return false;
	}

	public void Clear()
	{
		Objects.Clear();
	}
}
```

### Разбор LinkedGameObjectBuilder

Это «обходчик графа связей» — он находит все объекты, связанные с выбранным, через физические соединения и ручные ссылки.

- **`Add(obj)`** — добавляет один объект с проверками:
  - `!obj.IsValid()` — объект существует
  - `"world"` — не мир
  - `Objects.Contains(obj)` — ещё не добавлен (защита от бесконечного цикла)
  - `MapInstance` — не экземпляр карты
  - `RejectPlayers` — не содержит игрока как потомка

- **`AddConnected(source)`** — рекурсивный обход:
  1. Берёт корневой объект (`source.Root`)
  2. Пробует добавить его (`Add`) — если не удаётся (уже добавлен или мир), выходит
  3. Перебирает все `Rigidbody` и их `Joints` — добавляет объекты с обеих сторон соединения
  4. Перебирает все `Collider` и их `Joints` — аналогично
  5. Перебирает все `ManualLink` — добавляет связанные объекты (те самые связи от LinkerTool)

- **Рекурсия с защитой от циклов** — метод `Add()` возвращает `false` если объект уже в списке, что прерывает рекурсию `AddConnected()`. Это обеспечивает корректный обход даже при циклических связях (A↔B↔C↔A).

- **`HasDescendantWithTag()`** — рекурсивный поиск тега среди потомков. Используется для фильтрации объектов с игроками.


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[12.01 — Компонент: Владение объектом (Ownable) 🛡️](12_01_Ownable.md)** — начало следующей фазы.
