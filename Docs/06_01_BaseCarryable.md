# 06.01 — Базовый переносимый предмет (BaseCarryable) 🎒

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём базовый класс `BaseCarryable` — фундамент для любого предмета, который игрок может держать в руках: оружие, инструменты, камеры и т.д. Также определяем запись `TraceAttackInfo`, описывающую результат трассировочной атаки.

## Зачем это нужно?

- **Единый интерфейс**: все переносимые предметы (оружие, инструменты) наследуют общее поведение — отображение, рисование HUD, управление моделями.
- **Инвентарь**: свойства `DisplayName`, `DisplayIcon`, `InventorySlot`, `Value` позволяют системе инвентаря сортировать и показывать предметы.
- **Владение**: через `Owner` / `HasOwner` предмет знает, какому игроку он принадлежит.
- **Модели**: `ViewModel` и `WorldModel` — переключение между первым и третьим лицом.
- **Атаки через трассировку**: `TraceAttackInfo` — компактная структура для передачи результата луча (цель, урон, позиция) и метод `TraceAttack` на хосте, который наносит урон и применяет силу.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `record struct TraceAttackInfo` | Неизменяемая структура с данными атаки. Фабричный метод `From()` создаёт её из `SceneTraceResult`, добавляя теги хитбокса. |
| `IKillIcon` | Интерфейс для отображения иконки убийства в килфиде. |
| `WeaponModel` (свойство) | Возвращает компонент `WeaponModel` из `ViewModel` или `WorldModel`, учитывая текущий вид камеры (firstperson / thirdperson). |
| `Owner` | Ищет компонент `Player` в родительской иерархии через `GetComponentInParent`. |
| `MuzzleGameObject` | `[Property]` — опциональная явная точка вылета. Используется, когда нет `WeaponModel` (например, в режиме сиденья/standalone). Если не задан — берётся muzzle из `WeaponModel`, иначе сам `GameObject`. |
| `MuzzleTransform` | Точка откуда вылетают эффекты выстрела. Новый порядок разрешения: 1) muzzle из `WeaponModel` (если валиден), 2) явный `MuzzleGameObject`, 3) сам `GameObject`. |
| `OnEnabled / OnDisabled` | Создание/уничтожение мировой и вью-моделей при активации. |
| `OnFrameUpdate` | Каждый кадр определяет, нужна ли вью-модель (первое лицо) или нет (третье лицо). |
| `OnPlayerUpdate → OnControl` | Цепочка вызовов для обработки ввода владельца. |
| `[Rpc.Host] TraceAttack` | RPC-метод: клиент сообщает серверу об атаке, сервер наносит урон через `IDamageable` и прикладывает силу к `Rigidbody`. |
| `[Sync, Change]` на `IsItem` | Синхронизируемое свойство с обратным вызовом `OnItemVisibility` для показа/скрытия `DroppedGameObject`. |

## Создай файл

**Путь:** `Code/Game/Weapon/BaseCarryable/BaseCarryable.cs`

```csharp
using Sandbox.Rendering;

/// <summary>
/// Info about a trace attack. It's a struct so we can add to it without updating params everywhere.
/// </summary>
/// <param name="Target"></param>
/// <param name="Damage"></param>
/// <param name="Tags"></param>
/// <param name="Position"></param>
/// <param name="Origin"></param>
public record struct TraceAttackInfo( GameObject Target, float Damage, TagSet Tags = null, Vector3 Position = default, Vector3 Origin = default )
{
	/// <summary>
	/// Constructs a <see cref="TraceAttackInfo"/> from a trace and input damage.
	/// </summary>
	public static TraceAttackInfo From( SceneTraceResult tr, float damage, TagSet tags = default, bool localise = true )
	{
		tags ??= new();

		if ( localise && tr.Hitbox?.Tags is not null )
		{
			tags.Add( tr.Hitbox?.Tags );
		}

		return new TraceAttackInfo( tr.GameObject, damage, tags, tr.HitPosition, tr.StartPosition );
	}
}

public partial class BaseCarryable : Component, IKillIcon
{
	[Property, Feature( "Inventory" )] public string DisplayName { get; set; } = "My Weapon";
	[Property, Feature( "Inventory" ), TextArea] public Texture DisplayIcon { get; set; }

	/// <summary>
	/// The prefab to spawn in the world when this item is dropped from the inventory.
	/// </summary>
	[Property, Feature( "Inventory" )] public GameObject ItemPrefab { get; set; }

	public GameObject ViewModel { get; protected set; }
	public GameObject WorldModel { get; protected set; }

	/// <summary>
	/// Optional explicit muzzle point. Used when no WeaponModel is present (e.g. standalone/seat mode).
	/// If unset, falls back to the WeaponModel muzzle or the weapon's own GameObject.
	/// </summary>
	[Property] public GameObject MuzzleGameObject { get; set; }

	/// <summary>
	/// Used for overriding the display icon
	/// </summary>
	public virtual string InventoryIconOverride => null;

	/// <summary>
	/// Wether this weapon should be avoided when determining an item to swap to
	/// </summary>
	public virtual bool ShouldAvoid => false;

	/// <summary>
	/// If true the game should hide the hud when holding this weapon. Useful for cameras, or scopes.
	/// </summary>
	public virtual bool WantsHideHud => false;

	/// <summary>
	/// The value of this weapon, used for auto-switch.
	/// </summary>
	[Property, Feature( "Inventory" )] public int Value { get; set; } = 0;

	/// <summary>
	/// Gets a reference to the weapon model for this weapon - if there's a viewmodel, pick the viewmodel, if not, world model.
	/// </summary>
	public WeaponModel WeaponModel
	{
		get
		{
			var go = ViewModel;

			if ( Scene.Camera.RenderExcludeTags.Contains( "firstperson" ) ) go = default;

			if ( !go.IsValid() ) go = WorldModel;
			if ( !go.IsValid() ) return null;

			return go.GetComponent<WeaponModel>();
		}
	}

	/// <summary>
	/// The owner of this carriable
	/// </summary>
	public Player Owner
	{
		get
		{
			return GetComponentInParent<Player>( true );
		}
	}

	public bool HasOwner => Owner.IsValid();

	/// <summary>
	/// Where shoot effects come from. Either the point on the world model or the viewmodel, whichever is currently being used.
	/// Falls back to <see cref="MuzzleGameObject"/> (if set) and finally to this component's own GameObject.
	/// </summary>
	public GameObject MuzzleTransform
	{
		get
		{
			if ( WeaponModel?.MuzzleTransform.IsValid() ?? false ) return WeaponModel.MuzzleTransform;
			if ( MuzzleGameObject.IsValid() ) return MuzzleGameObject;
			return GameObject;
		}
	}

	/// <summary>
	/// The inventory slot this item is assigned to, or -1 if unassigned.
	/// Set at runtime when picked up.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public int InventorySlot { get; set; } = -1;

	/// <summary>
	/// This is shite
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnItemVisibility ) )]
	public bool IsItem { get; set; } = true;

	private void OnItemVisibility( bool oldVal, bool newVal )
	{
		if ( DroppedGameObject.IsValid() )
			DroppedGameObject.Enabled = newVal;
	}

	/// <summary>
	/// Can we switch to this?
	/// </summary>
	/// <returns></returns>
	public virtual bool CanSwitch()
	{
		return true;
	}

	protected override void OnEnabled()
	{
		CreateWorldModel();
	}

	protected override void OnDisabled()
	{
		DestroyWorldModel();
		DestroyViewModel();
	}

	protected override void OnUpdate()
	{
		var player = Owner;
		var controller = player?.Controller;
		if ( controller is null ) return;

		if ( player.IsLocalPlayer )
		{
			if ( Scene.Camera is null )
				return;

			var hud = Scene.Camera.Hud;

			var aimPos = Screen.Size * 0.5f;

			if ( controller.ThirdPerson )
			{
				var tr = Scene.Trace.Ray( controller.EyeTransform.ForwardRay, 4096 )
									.IgnoreGameObjectHierarchy( controller.GameObject )
									.Run();

				aimPos = Scene.Camera.PointToScreenPixels( tr.EndPosition );
			}

			if ( !Scene.Camera.RenderExcludeTags.Has( "ui" ) )
			{
				DrawHud( hud, aimPos );
			}
		}
	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// nothing
	}

	/// <summary>
	/// Called when added to the player's inventory
	/// </summary>
	/// <param name="player"></param>
	public virtual void OnAdded( Player player )
	{
		// nothing
	}

	/// <summary>
	/// Called every frame, when active
	/// </summary>
	public virtual void OnFrameUpdate( Player player )
	{
		if ( player is null ) return;

		if ( !player.Controller.ThirdPerson )
		{
			CreateViewModel();
		}
		else
		{
			DestroyViewModel();
		}

		GameObject.Network.Interpolation = false;
	}

	/// <summary>
	/// Called every frame, on the owning player's client.
	/// </summary>
	public virtual void OnPlayerUpdate( Player player )
	{
		Assert.True( !IsProxy );

		try
		{
			OnControl( player );
		}
		catch ( System.Exception e )
		{
			Log.Error( e, $"{GetType().Name}.OnControl {e.Message}" );
		}
	}

	/// <summary>
	/// Called every update, scoped to the owning player
	/// </summary>
	/// <param name="player"></param>
	public virtual void OnControl( Player player )
	{
	}

	/// <summary>
	/// Called when setting up the camera - use this to apply effects on the camera based on this carriable
	/// </summary>
	/// <param name="player"></param>
	/// <param name="camera"></param>
	public virtual void OnCameraSetup( Player player, Sandbox.CameraComponent camera )
	{
	}

	/// <summary>
	/// Can directly influence the player's eye angles here
	/// </summary>
	/// <param name="player"></param>
	/// <param name="angles"></param>
	public virtual void OnCameraMove( Player player, ref Angles angles )
	{
	}

	/// <summary>
	/// Run a trace related attack with some set information.
	/// This is targeted to the host who then does things.
	/// </summary>
	/// <param name="attack"></param>
	[Rpc.Host]
	public void TraceAttack( TraceAttackInfo attack )
	{
		if ( !attack.Target.IsValid() )
			return;

		// Use owner as attacker when held by a player; fall back to the weapon itself (standalone mode)
		var attacker = HasOwner ? Owner.GameObject : GameObject;

		var damagable = attack.Target.GetComponentInParent<IDamageable>();
		if ( damagable is not null )
		{
			var info = new DamageInfo( attack.Damage, attacker, GameObject );
			info.Position = attack.Position;
			info.Origin = attack.Origin;
			info.Tags = attack.Tags;

			damagable.OnDamage( info );
		}

		if ( attack.Target.GetComponentInChildren<Rigidbody>() is var rb && rb.IsValid() )
		{
			rb.ApplyForce( (attack.Position - attack.Origin) * 1000f );
		}
	}

	/// <summary>
	/// Is this item currently being used? When true, prevents auto-switching away on item pickup etc.
	/// </summary>
	public virtual bool IsInUse()
	{
		return false;
	}

	public virtual void OnPlayerDeath( IPlayerEvent.DiedParams args )
	{
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `TraceAttackInfo.From()` корректно создаёт структуру из `SceneTraceResult`
- [ ] `Owner` возвращает `Player` из родительской иерархии
- [ ] `WeaponModel` выбирает между ViewModel и WorldModel в зависимости от камеры
- [ ] `TraceAttack` (RPC на хосте) наносит урон и прикладывает силу
- [ ] При включении/выключении компонента создаются/уничтожаются модели


---

## ➡️ Следующий шаг

Переходи к **[06.02 — ViewModel переносимого предмета (BaseCarryable.ViewModel) 👁️](06_02_BaseCarryable_Models.md)**.
