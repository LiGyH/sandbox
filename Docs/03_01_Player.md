# 03.01 — Главный компонент игрока (Player) 🧑

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
> - [Как это работает?](#как-это-работает)
> - [Создай файл](#создай-файл)
> - [Построчное объяснение ключевых участков](#построчное-объяснение-ключевых-участков)
> - [Проверка](#проверка)
> - [Следующий файл](#следующий-файл)

## Что мы делаем?

Создаём **центральный компонент игрока** — `Player`. Это самый важный класс в игре: он хранит здоровье, броню, обрабатывает урон, создаёт рэгдолл при смерти, управляет вводом и соединяет все подсистемы (инвентарь, камера, физика).

## Как это работает?

### Partial class — файл разбит на части

`Player` объявлен как `partial class`, то есть его код разложен по нескольким файлам:
- `Player.cs` — основная логика (здоровье, урон, смерть, ввод)
- `Player.Camera.cs` — настройка камеры
- `Player.Ammo.cs` — заглушка для патронов
- `Player.ConsoleCommands.cs` — консольные команды
- `Player.Undo.cs` — доступ к системе отмены

### Реализуемые интерфейсы

```
Component.IDamageable     — получение урона
PlayerController.IEvents  — обработка ввода и камеры
ISaveEvents               — сохранение/загрузка
IKillSource               — информация об убийце в ленте фрагов
```

### Ключевые свойства

| Свойство | Тип | Назначение |
|----------|-----|-----------|
| `Health` | `float` | Текущее HP (0–100), синхронизируется по сети |
| `MaxHealth` | `float` | Макс. HP |
| `Armour` / `MaxArmour` | `float` | Броня поглощает урон до того, как он дойдёт до HP |
| `PlayerData` | `PlayerData` | Постоянные данные (kills, deaths, SteamId) |
| `Controller` | `PlayerController` | Движок персонажа (RequireComponent) |
| `Body` | `GameObject` | Модель тела игрока |
| `EyeTransform` | `Transform` | Позиция и направление глаз |

### Система урона (OnDamage)

1. Проверяем: жив ли игрок, есть ли PlayerData, не включён ли godmode
2. Игнорируем урон от столкновений с миром (impact), если игрок не летел
3. Хедшот (`DamageTags.Headshot`) — урон ×2
4. Броня поглощает урон: `Armour -= damage`, остаток идёт в `Health`
5. Рассылаем `NotifyOnDamage` — кровь, звук, события
6. Если `Health < 1` — вызываем `Kill()`

### Смерть (Kill)

1. Звук «flatline» для самого игрока
2. Рассылаем `NotifyDeath` — лента убийств
3. Убираем оружие из рук
4. Если урон был от взрыва/crush/gib — разлетаемся на куски (`Gib`)
5. Иначе — создаём рэгдолл (`CreateRagdoll`)
6. Создаём «призрака» (`PlayerObserver`) для наблюдения за телом
7. Уничтожаем `GameObject` игрока

### Рэгдолл (CreateRagdoll)

Метод помечен `[Rpc.Broadcast]` — выполняется на всех клиентах:
1. Создаём новый `GameObject("Ragdoll")`
2. Копируем `SkinnedModelRenderer` с игрока
3. Копируем одежду (дочерние объекты с тегом `"clothing"`)
4. Добавляем `ModelPhysics` — тело падает под действием физики
5. Добавляем `DeathCameraTarget` — камера следит за телом
6. Копируем масштабы костей (для нестандартных моделей)

### Ввод (OnControl)

Вызывается каждый кадр через `PlayerController.IEvents.PreInput`:
- **Die** — самоубийство
- **Jump ×2** (в течение 0.3с) — переключение Noclip
- **Undo** — отмена последнего действия
- Передаёт управление в `PlayerInventory.OnControl()` (оружие, слоты)

## Создай файл

Путь: `Code/Player/Player.cs`

```csharp
using Sandbox.CameraNoise;
using Sandbox.Movement;
using Sandbox.UI.Inventory;

/// <summary>
/// Holds player information like health
/// </summary>
public sealed partial class Player : Component, Component.IDamageable, PlayerController.IEvents, ISaveEvents, IKillSource
{
	public static Player FindLocalPlayer() => Game.ActiveScene.GetAll<Player>().FirstOrDefault( x => x.IsLocalPlayer );
	public static T FindLocalWeapon<T>() where T : BaseCarryable => FindLocalPlayer()?.GetComponentInChildren<T>( true );
	public static T FindLocalToolMode<T>() where T : ToolMode => FindLocalPlayer()?.GetComponentInChildren<T>( true );

	[RequireComponent] public PlayerController Controller { get; set; }
	[Property] public GameObject Body { get; set; }
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxHealth { get; set; } = 100;

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Armour { get; set; } = 0;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxArmour { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] public PlayerData PlayerData { get; set; }

	public Transform EyeTransform
	{
		get
		{
			if ( !Controller.IsValid() )
			{
				Log.Warning( $"Invalid Controller for {this.GameObject}" );
				return default;
			}
			return Controller.EyeTransform;
		}
	}

	public bool IsLocalPlayer => !IsProxy;
	public Guid PlayerId => PlayerData.IsValid() ? PlayerData.PlayerId : Guid.Empty;
	public long SteamId => PlayerData.IsValid() ? PlayerData.SteamId : 0;
	public string DisplayName => PlayerData.IsValid() ? PlayerData.DisplayName : "Unknown";

	// IKillSource
	string IKillSource.DisplayName => DisplayName;
	long IKillSource.SteamId => SteamId;
	void IKillSource.OnKill( GameObject victim )
	{
		PlayerData.Kills++;
		PlayerData.AddStat( victim?.GetComponent<Player>().IsValid() ?? false ? "kills" : "kills.npc" );
	}

	/// <summary>
	/// True if the player wants the HUD not to draw right now
	/// </summary>
	public bool WantsHideHud
	{
		get
		{
			var freeCam = Scene.Get<FreeCamGameObjectSystem>();
			if ( freeCam.IsActive )
				return true;

			var weapon = GetComponent<PlayerInventory>()?.ActiveWeapon;
			if ( weapon.IsValid() && weapon.WantsHideHud )
				return true;

			return false;
		}
	}

	protected override void OnStart()
	{
		var targets = Scene.GetAllComponents<DeathCameraTarget>()
			.Where( x => x.Connection == Network.Owner );

		// We don't care about spectating corpses once we spawn
		foreach ( var t in targets )
		{
			t.GameObject.Destroy();
		}
	}

	/// <summary>
	/// Try to inherit transforms from the player onto its new ragdoll
	/// </summary>
	/// <param name="ragdoll"></param>
	private void CopyBoneScalesToRagdoll( GameObject ragdoll )
	{
		// we are only interested in the bones of the player, not anything that may be attached to it.
		var playerRenderer = Body.GetComponent<SkinnedModelRenderer>();
		var bones = playerRenderer.Model.Bones;

		var ragdollRenderer = ragdoll.GetComponent<SkinnedModelRenderer>();
		ragdollRenderer.CreateBoneObjects = true;

		var ragdollObjects = ragdoll.GetAllObjects( true ).ToLookup( x => x.Name );

		foreach ( var bone in bones.AllBones )
		{
			var boneName = bone.Name;

			if ( !ragdollObjects.Contains( boneName ) )
				continue;

			var boneObject = playerRenderer.GetBoneObject( boneName );
			if ( !boneObject.IsValid() )
			{
				continue;
			}

			var boneOnRagdoll = ragdollObjects[boneName].FirstOrDefault();

			if ( boneOnRagdoll.IsValid() && boneObject.WorldScale != Vector3.One )
			{
				boneOnRagdoll.Flags = boneOnRagdoll.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				boneOnRagdoll.WorldScale = boneObject.WorldScale;

				var z = boneOnRagdoll.Parent;
				z.Flags = z.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				z.WorldScale = boneObject.WorldScale;
			}
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void CreateRagdoll()
	{
		if ( !Controller.Renderer.IsValid() )
			return;

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = WorldTransform;

		var mainBody = go.Components.Create<SkinnedModelRenderer>();
		mainBody.CopyFrom( Controller.Renderer );
		mainBody.UseAnimGraph = false;

		// copy the clothes
		foreach ( var clothing in Controller.Renderer.GameObject.Children.Where( x => x.Tags.Has( "clothing" ) ).SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			if ( !clothing.IsValid() ) continue;

			var newClothing = new GameObject( true, clothing.GameObject.Name );
			newClothing.Parent = go;

			var item = newClothing.Components.Create<SkinnedModelRenderer>();
			item.CopyFrom( clothing );
			item.BoneMergeTarget = mainBody;
		}

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = mainBody.Model;
		physics.Renderer = mainBody;
		physics.CopyBonesFrom( Controller.Renderer, true );

		var corpse = go.AddComponent<DeathCameraTarget>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;

		CopyBoneScalesToRagdoll( go );
	}

	void CreateRagdollAndGhost()
	{
		var go = new GameObject( false, "Observer" );
		go.Components.Create<PlayerObserver>();
		go.NetworkSpawn( Network.Owner );
	}

	/// <summary>
	/// Broadcasts death to other players
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void NotifyDeath( IPlayerEvent.DiedParams args )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnDied( args ) );

		if ( args.Attacker == GameObject )
		{
			IPlayerEvent.PostToGameObject( GameObject, x => x.OnSuicide() );
		}
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	private void Flatline()
	{
		Sound.Play( "audio/sounds/flatline.sound" );
	}

	private void Ghost()
	{
		CreateRagdollAndGhost();
	}

	/// <summary>
	/// Called on the host when a player dies
	/// </summary>
	void Kill( in DamageInfo d )
	{
		//
		// Play the flatline sound on the owner
		//
		if ( IsLocalPlayer )
		{
			Flatline();
		}

		//
		// Let everyone know about the death
		//

		NotifyDeath( new IPlayerEvent.DiedParams() { Attacker = d.Attacker } );

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() )
		{
			inventory.SwitchWeapon( null );
		}


		if ( d.Tags.HasAny( DamageTags.Crush, DamageTags.Explosion, DamageTags.GibAlways ) )
		{
			Gib( d.Position, d.Origin );
		}
		else
		{
			CreateRagdoll();
		}

		//
		// Ghost and say goodbye to the player
		//
		PlayerData?.MarkForRespawn();
		Ghost();
		GameObject.Destroy();
	}

	[Rpc.Host]
	public void EquipBestWeapon()
	{
		var inventory = GetComponent<PlayerInventory>();

		if ( inventory.IsValid() )
			inventory.SwitchWeapon( inventory.GetBestWeapon() );
	}

	void PlayerController.IEvents.PreInput()
	{
		OnControl();
	}

	private RealTimeSince _timeSinceJumpPressed;

	void OnControl()
	{
		if ( Input.UsingController )
		{
			Controller.UseInputControls = !(Input.Down( "SpawnMenu" ) || Input.Down( "InspectMenu" ));
		}
		else
		{
			Controller.UseInputControls = true;
		}

		if ( Input.Pressed( "die" ) )
		{
			KillSelf();
			return;
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( _timeSinceJumpPressed < 0.3f )
			{
				if ( GetComponent<NoclipMoveMode>( true ) is { } noclip )
				{
					noclip.Enabled = !noclip.Enabled;
				}
			}

			_timeSinceJumpPressed = 0;
		}

		if ( Input.Pressed( "undo" ) )
		{
			ConsoleSystem.Run( "undo" );
		}

		GetComponent<PlayerInventory>()?.OnControl();

		Scene.Get<Inventory>()?.HandleInput();
	}

	[ConCmd( "sbdm.dev.sethp", ConVarFlags.Cheat )]
	private static void Dev_SetHp( int hp )
	{
		FindLocalPlayer().Health = hp;
	}

	private SoundHandle _dmgSound;

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void NotifyOnDamage( IPlayerEvent.DamageParams args )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnDamage( args ) );

		Effects.Current.SpawnBlood( args.Position, (args.Origin - args.Position).Normal, args.Damage );

		if ( IsLocalPlayer )
		{
			_dmgSound?.Stop();

			if ( args.Tags.Contains( DamageTags.Shock ) )
			{
				_dmgSound = Sound.Play( "damage_taken_shock" );
			}
			else
			{
				_dmgSound = Sound.Play( "damage_taken_shot" );
			}
		}
	}

	public void OnDamage( in DamageInfo dmg )
	{
		if ( Health < 1 ) return;
		if ( !PlayerData.IsValid() ) return;
		if ( PlayerData.IsGodMode ) return;

		//
		// Ignore impact damage from the world, for now
		//
		if ( dmg.Tags.Contains( "impact" ) )
		{
			// Was this fall damage? If so, we can bail out here
			if ( Controller.Velocity.Dot( Vector3.Down ) > 10 )
				return;

			// We were hit by some flying object, or flew into a wall, 
			// so lets take that damage.
		}


		var damage = dmg.Damage;
		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			damage *= 2;

		if ( Armour > 0 )
		{
			float remainingDamage = damage - Armour;
			Armour = Math.Max( 0, Armour - damage );
			damage = Math.Max( 0, remainingDamage );
		}

		Health -= damage;

		NotifyOnDamage( new IPlayerEvent.DamageParams()
		{
			Damage = damage,
			Attacker = dmg.Attacker,
			Weapon = dmg.Weapon,
			Tags = dmg.Tags,
			Position = dmg.Position,
			Origin = dmg.Origin,
		} );

		// We didn't die
		if ( Health >= 1 ) return;

		GameManager.Current.OnDeath( this, dmg );

		Health = 0;
		Kill( dmg );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void Gib( Vector3 hitPos, Vector3 origin )
	{
		var gibList = new List<PlayerGib>( GetComponentsInChildren<PlayerGib>( true ) );

		DeathCameraTarget target = null;
		foreach ( var g in gibList )
		{
			// Death camera target is the first gib
			if ( !target.IsValid() )
			{
				target = g.AddComponent<DeathCameraTarget>();
				target.Connection = Network.Owner;
				target.Created = DateTime.Now;
			}

			g.Gib( origin, hitPos, noShrink: true );
		}

		Effects.Current.SpawnBlood( WorldPosition, Vector3.Up, 500.0f );
	}

	void PlayerController.IEvents.OnLanded( float distance, Vector3 impactVelocity )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnLand( distance, impactVelocity ) );

		var player = Components.Get<Player>();
		if ( !player.IsValid() ) return;

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		new Punch( new Vector3( 0.3f * distance, Random.Shared.Float( -1, 1 ), Random.Shared.Float( -1, 1 ) ), 1.0f, 1.5f, 0.7f );
	}

	void PlayerController.IEvents.OnJumped()
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnJump() );

		var player = Components.Get<Player>();

		if ( Controller.ThirdPerson || !player.IsLocalPlayer ) return;

		new Punch( new Vector3( -20, 0, 0 ), 0.5f, 2.0f, 1.0f );
	}

	public T GetWeapon<T>() where T : BaseCarryable
	{
		return GetComponent<PlayerInventory>().GetWeapon<T>();
	}

	public void SwitchWeapon<T>() where T : BaseCarryable
	{
		var weapon = GetWeapon<T>();
		if ( weapon == null ) return;

		GetComponent<PlayerInventory>().SwitchWeapon( weapon );
	}

	public override void OnParentDestroy()
	{
		// When parent is destroyed, unparent the player to avoid destroying it
		GameObject.SetParent( null, true );
	}

	void ISaveEvents.AfterLoad( string filename )
	{
		if ( !Body.IsValid() ) return;

		var dresser = Body.GetComponentInChildren<Dresser>( true );
		if ( !dresser.IsValid() ) return;

		// Apply clothing after load
		_ = ReapplyClothingAfterLoad( dresser );
	}

	private async Task ReapplyClothingAfterLoad( Dresser dresser )
	{
		await dresser.Apply();
		GameObject.Network.Refresh();
	}
}
```

## Построчное объяснение ключевых участков

### Статические хелперы (строки 11–13)

```csharp
public static Player FindLocalPlayer() => ...
public static T FindLocalWeapon<T>() where T : BaseCarryable => ...
public static T FindLocalToolMode<T>() where T : ToolMode => ...
```

Удобные методы для быстрого доступа к локальному игроку и его оружию из любого места кода. `GetAll<Player>()` ищет по всей сцене.

### Sync и сетевая синхронизация (строки 17–21)

```csharp
[Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
```

`SyncFlags.FromHost` означает: **только хост может менять это значение**, а все клиенты получают обновления автоматически. Это защита от читеров — клиент не может сам выставить себе 100 HP.

### Создание рэгдолла (CreateRagdoll)

```csharp
[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
void CreateRagdoll() { ... }
```

- `Rpc.Broadcast` — вызывается на всех клиентах
- `NetFlags.HostOnly` — только хост может инициировать вызов
- `NetFlags.Reliable` — гарантия доставки (TCP-подобное поведение)

### Noclip по двойному прыжку (строки 253–263)

```csharp
if ( _timeSinceJumpPressed < 0.3f )
{
    if ( GetComponent<NoclipMoveMode>( true ) is { } noclip )
    {
        noclip.Enabled = !noclip.Enabled;
    }
}
_timeSinceJumpPressed = 0;
```

`RealTimeSince` — специальный тип s&box, который автоматически считает время. Если между двумя нажатиями Jump прошло менее 0.3 секунды — включаем/выключаем Noclip.

## Проверка

В s&box Editor:
1. Найди `Player` в сцене → должен быть компонент `Player` с полями Health, MaxHealth, Armour
2. Зайди в игру, получи урон → Health уменьшается
3. Умри → появляется рэгдолл, камера следит за телом
4. Двойной прыжок → включается/выключается Noclip

## Следующий файл

Переходи к **03.02 — Камера игрока (Player.Camera)**.
