# 03.08 — Инвентарь игрока (PlayerInventory) 🎒

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
> - [Архитектура](#архитектура)
> - [Основные операции](#основные-операции)
> - [Система Loadout (сохранение/загрузка набора)](#система-loadout-сохранениезагрузка-набора)
> - [Создай файл](#создай-файл)
> - [Ключевые паттерны](#ключевые-паттерны)
> - [Проверка](#проверка)
> - [Следующий файл](#следующий-файл)

## Что мы делаем?

Создаём **систему инвентаря** — самый большой компонент в системе игрока (865 строк). Он управляет:
- Слотами оружия (до 6 штук)
- Подбором, выбросом и переключением оружия
- Сохранением/загрузкой loadout'а (набора оружия)
- Автопереключением на лучшее оружие
- Пресетами (предустановленные наборы)

## Архитектура

### Слоты

```
Slot 0: Physgun      (по умолчанию)
Slot 1: Toolgun      (по умолчанию)
Slot 2: —
Slot 3: —
Slot 4: —
Slot 5: —            (MaxSlots = 6)
```

Каждое оружие (`BaseCarryable`) имеет `InventorySlot` — номер слота. Инвентарь хранит оружие как **дочерние объекты** на GameObject игрока.

### Активное оружие

```csharp
[Sync( SyncFlags.FromHost ), Change] public BaseCarryable ActiveWeapon { get; private set; }
```

- `SyncFlags.FromHost` — только хост меняет активное оружие
- `[Change]` — при смене вызывается `OnActiveWeaponChanged`:
  - Старое оружие: `Enabled = false` (прячем)
  - Новое оружие: `Enabled = true` (показываем)

## Основные операции

### Подбор оружия (Pickup)

Три перегрузки:
1. `Pickup(string prefabName)` — по имени префаба, автослот
2. `Pickup(GameObject prefab, int slot)` — по объекту и слоту
3. `Pickup(string prefabName, int slot)` — по имени и слоту

Алгоритм:
1. Проверяем: мы хост? Есть ли свободный слот?
2. Если уже есть такое оружие → добавляем только патроны (`AddReserveAmmo`)
3. Клонируем префаб → делаем дочерним объектом игрока через `SetParent( GameObject, false )` и обнуляем `LocalTransform`, чтобы оружие было ровно на хозяине
4. **Удаляем GameObject из всех undo-стеков** (`UndoSystem.Current.Remove`) — иначе кто-то может «отменить» спавн прямо у нас из рук
5. `NetworkSpawn` — регистрируем в сети (владелец = владелец игрока)
6. Вызываем `OnAdded` на оружии и `IPlayerEvent.OnPickup`
7. Если нужно — автопереключение (`ShouldAutoswitchTo`)

### Выброс оружия (Drop)

```csharp
public bool Drop( BaseCarryable weapon )
```

1. Если это активное оружие → сначала убираем из рук
2. Два варианта:
   - **DroppedWeapon** — создаём свежий клон из префаба (чистое состояние)
   - **Обычное** — создаём из `ItemPrefab`
3. Назначаем владельца (`Ownable.Set`) и добавляем тег `"removable"` **до** `NetworkSpawn` — так клиенты сразу видят правильного владельца и набор тегов, а Cleanup System (`CleanupSystem`, тег `"removable"`) корректно подхватит дроп при общей очистке
4. Запускаем `NetworkSpawn` и физику: скорость игрока + бросок вперёд + случайное вращение
5. Переключаемся на лучшее оставшееся оружие

### Переключение оружия (SwitchWeapon)

```csharp
public void SwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
```

- Если `weapon == ActiveWeapon` и `allowHolster` → убираем оружие
- Иначе — просто меняем `ActiveWeapon`
- Клиент вызывает `HostSwitchWeapon` → хост обновляет → сеть синхронизирует

### Автопереключение

```csharp
private bool ShouldAutoswitchTo( BaseCarryable item )
```

Переключаемся автоматически, если:
- Нет текущего оружия
- `GamePreferences.AutoSwitch` включен
- Текущее оружие не используется (`!IsInUse`)
- Новое оружие ценнее (`item.Value > ActiveWeapon.Value`)
- Новое оружие имеет патроны

## Система Loadout (сохранение/загрузка набора)

### Сериализация

```csharp
private struct LoadoutEntry
{
    public string PrefabPath { get; set; }
    public int Slot { get; set; }
    public string SpawnerDataPayload { get; set; }
}
```

Сохраняем: путь к префабу + номер слота + данные спавнера (если это SpawnerWeapon).

### Где хранится

- **Локальный игрок** → `LocalData.Set("hotbar", json)` (файл на диске)
- **Удалённый игрок** → хост отправляет через `PushLoadoutToClient`
- **Сохранение карты** → `SaveSystem.SetMetadata("Loadout_{steamId}", json)`

### При спавне

```csharp
void IPlayerEvent.OnSpawned()
{
    if ( Player.IsLocalPlayer )
    {
        var json = LocalData.Get<string>( "hotbar" );
        if ( !string.IsNullOrEmpty( json ) )
        {
            GiveLoadoutWeapons( json );
            return;
        }
    }
    else
    {
        RequestClientLoadout();  // RPC — запрашиваем loadout с клиента
    }
    GiveDefaultWeapons();  // fallback: physgun + toolgun + camera
}
```

### Пресеты

```csharp
public static void SaveLoadoutPreset( string name, string loadoutJson )
public static void DeleteLoadoutPreset( string name )
public void SwitchToPreset( string loadoutJson )
public void ResetToDefault()
```

Игрок может сохранять именованные наборы оружия и переключаться между ними.

## Создай файл

Путь: `Code/Player/PlayerInventory.cs`

```csharp
using Sandbox.Citizen;

public sealed class PlayerInventory : Component, IPlayerEvent, ISaveEvents
{
	[Property] public int MaxSlots { get; set; } = 6;

	[RequireComponent] public Player Player { get; set; }

	/// <summary>
	/// All weapons currently in the inventory, ordered by slot.
	/// Returns an IEnumerable so callers can iterate without an extra allocation.
	/// </summary>
	public IEnumerable<BaseCarryable> Weapons => GetComponentsInChildren<BaseCarryable>( true )
		.OrderBy( x => x.InventorySlot );

	[Sync( SyncFlags.FromHost ), Change] public BaseCarryable ActiveWeapon { get; private set; }

	public void OnActiveWeaponChanged( BaseCarryable oldWeapon, BaseCarryable newWeapon )
	{
		if ( oldWeapon.IsValid() )
			oldWeapon.GameObject.Enabled = false;

		if ( newWeapon.IsValid() )
		{
			newWeapon.GameObject.Enabled = true;
			newWeapon.SetDropped( false );
		}
	}

	/// <summary>
	/// Returns the weapon in the given slot, or null if the slot is empty.
	/// </summary>
	public BaseCarryable GetSlot( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return null;
		return GetComponentsInChildren<BaseCarryable>( true )
			.FirstOrDefault( x => x.InventorySlot == slot );
	}

	/// <summary>
	/// Returns the first empty slot index, or -1 if the inventory is full.
	/// </summary>
	public int FindEmptySlot()
	{
		var occupied = GetComponentsInChildren<BaseCarryable>( true )
			.Where( x => x.InventorySlot >= 0 )
			.Select( x => x.InventorySlot )
			.ToHashSet();

		for ( int i = 0; i < MaxSlots; i++ )
		{
			if ( !occupied.Contains( i ) )
				return i;
		}

		return -1;
	}

	public void GiveDefaultWeapons()
	{
		Pickup( "weapons/physgun/physgun.prefab", false );
		Pickup( "weapons/toolgun/toolgun.prefab", false );
		Pickup( "weapons/camera/camera.prefab", 8, false );
	}

	/// <summary>
	/// Activates the named tool mode, giving and equipping the toolgun first if the player doesn't have one.
	/// </summary>
	public void SetToolMode( string toolModeName )
	{
		if ( !Networking.IsHost )
		{
			HostSetToolMode( toolModeName );
			return;
		}

		if ( !HasWeapon<Toolgun>() )
			Pickup( "weapons/toolgun/toolgun.prefab", false );

		var toolgun = GetWeapon<Toolgun>();
		if ( !toolgun.IsValid() ) return;

		SwitchWeapon( toolgun );
		toolgun.SetToolMode( toolModeName );
	}

	[Rpc.Host]
	private void HostSetToolMode( string toolModeName )
	{
		SetToolMode( toolModeName );
	}

	public bool Pickup( string prefabName, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		var prefab = GameObject.GetPrefab( prefabName );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {prefabName}" );
			return false;
		}

		var slot = FindEmptySlot();
		if ( slot < 0 )
			return false;

		return Pickup( prefabName, slot, notice );
	}

	public bool HasWeapon( GameObject prefab )
	{
		var baseCarry = prefab.GetComponent<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		return Weapons.Where( x => x.GetType() == baseCarry.GetType() )
			.FirstOrDefault()
			.IsValid();
	}

	public bool HasWeapon<T>() where T : BaseCarryable
	{
		return GetWeapon<T>().IsValid();
	}

	public T GetWeapon<T>() where T : BaseCarryable
	{
		return Weapons.OfType<T>().FirstOrDefault();
	}

	public bool Pickup( GameObject prefab, bool notice = true )
	{
		var slot = FindEmptySlot();
		if ( slot < 0 )
			return false;

		return Pickup( prefab, slot, notice );
	}

	public bool Pickup( string prefabName, int targetSlot, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		var prefab = GameObject.GetPrefab( prefabName );
		if ( prefab is null )
		{
			Log.Warning( $"Prefab not found: {prefabName}" );
			return false;
		}

		if ( !Pickup( prefab, targetSlot, notice ) )
			return false;

		SaveLoadout();

		return true;
	}

	public bool Pickup( GameObject prefab, int targetSlot, bool notice = true )
	{
		if ( !Networking.IsHost )
			return false;

		if ( targetSlot < 0 || targetSlot >= MaxSlots )
			return false;

		var baseCarry = prefab.Components.Get<BaseCarryable>( true );
		if ( !baseCarry.IsValid() )
			return false;

		var existing = Weapons.Where( x => x.GameObject.Name == prefab.Name ).FirstOrDefault();
		if ( existing.IsValid() )
		{
			if ( existing is BaseWeapon existingWeapon && baseCarry is BaseWeapon pickupWeapon && existingWeapon.UsesAmmo )
			{
				if ( existingWeapon.ReserveAmmo >= existingWeapon.MaxReserveAmmo )
					return false;

				var ammoToGive = pickupWeapon.UsesClips ? pickupWeapon.ClipContents : pickupWeapon.StartingAmmo;
				existingWeapon.AddReserveAmmo( ammoToGive );

				if ( notice )
					OnClientPickup( existing, true );

				return true;
			}
		}

		// Reject if the target slot is already occupied
		var occupant = GetSlot( targetSlot );
		if ( occupant.IsValid() )
			return false;

		var clone = prefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false } );
		clone.NetworkSpawn( false, Network.Owner );

		//
		// Dropped variant components
		//
		{
			var cloneCarryable = clone.GetComponent<BaseCarryable>( true );
			cloneCarryable?.SetDropped( false );
		}

		var weapon = clone.GetComponent<BaseCarryable>( true );
		Assert.NotNull( weapon );

		weapon.InventorySlot = targetSlot;
		weapon.OnAdded( Player );

		IPlayerEvent.PostToGameObject( Player.GameObject, e => e.OnPickup( weapon ) );

		if ( notice )
			OnClientPickup( weapon );

		return true;
	}

	public void Take( BaseCarryable item, bool includeNotices )
	{
		var existing = Weapons.FirstOrDefault( x => x.GetType() == item.GetType() );
		if ( existing.IsValid() )
		{
			if ( existing is BaseWeapon existingWeapon && item is BaseWeapon pickupWeapon && existingWeapon.UsesAmmo )
			{
				if ( existingWeapon.ReserveAmmo < existingWeapon.MaxReserveAmmo )
				{
					existingWeapon.AddReserveAmmo( pickupWeapon.ClipContents );
					OnClientPickup( existing, true );
				}
			}

			item.DestroyGameObject();
			return;
		}

		// Reject if the inventory is full
		var slot = FindEmptySlot();
		if ( slot < 0 )
			return;

		item.GameObject.SetParent( GameObject, false );
		item.LocalTransform = global::Transform.Zero;
		item.InventorySlot = slot;
		item.GameObject.Enabled = false;

		// Удаляем оружие из всех undo-стеков, чтобы кто-нибудь не «отменил»
		// его прямо у нас из рук — например, если оружие изначально было
		// заспавнено через Spawnmenu и попало в чей-то undo-стек.
		UndoSystem.Current.Remove( item.GameObject );

		if ( Network.Owner is not null )
			item.Network.AssignOwnership( Network.Owner );
		else
			item.Network.DropOwnership();

		item.OnAdded( Player );

		IPlayerEvent.PostToGameObject( GameObject, e => e.OnPickup( item ) );
		OnClientPickup( item );
	}

	/// <summary>
	/// Drops the given weapon from the inventory.
	/// </summary>
	public bool Drop( BaseCarryable weapon )
	{
		if ( !Networking.IsHost )
		{
			HostDrop( weapon );
			return true;
		}

		if ( !weapon.IsValid() ) return false;
		if ( weapon.Owner != Player ) return false;

		var dropPosition = Player.EyeTransform.Position + Player.EyeTransform.Forward * 48f;
		var dropVelocity = Player.EyeTransform.Forward * 200f + Vector3.Up * 100f;

		// If this is the active weapon, holster first
		if ( ActiveWeapon == weapon )
		{
			SwitchWeapon( null, true );
		}

		// Weapons with a DroppedWeapon component: spawn a fresh prefab clone as server.
		// This avoids all ownership/state issues from the inventory copy.
		var droppedWeapon = weapon.GetComponent<DroppedWeapon>( true );
		if ( droppedWeapon.IsValid() )
		{
			var prefabSource = weapon.GameObject.PrefabInstanceSource;
			if ( !string.IsNullOrEmpty( prefabSource ) )
			{
				var prefab = GameObject.GetPrefab( prefabSource );
				if ( prefab.IsValid() )
				{
					var pickup = prefab.Clone( new CloneConfig
					{
						Transform = new Transform( dropPosition ),
						StartEnabled = true
					} );

					Ownable.Set( pickup, Player.Network.Owner );
					pickup.Tags.Add( "removable" );
					pickup.NetworkSpawn();

					if ( pickup.GetComponent<Rigidbody>() is { } rb )
					{
						rb.Velocity = Player.Controller.Velocity + dropVelocity;
						rb.AngularVelocity = Vector3.Random * 8.0f;
					}
				}
			}

			weapon.DestroyGameObject();
		}
		else
		{
			if ( !weapon.ItemPrefab.IsValid() ) return false;

			var pickup = weapon.ItemPrefab.Clone( new CloneConfig
			{
				Transform = new Transform( dropPosition ),
				StartEnabled = true
			} );

			Ownable.Set( pickup, Player.Network.Owner );
			pickup.Tags.Add( "removable" );
			pickup.NetworkSpawn();

			if ( pickup.GetComponent<Rigidbody>() is { } rb )
			{
				rb.Velocity = Player.Controller.Velocity + dropVelocity;
				rb.AngularVelocity = Vector3.Random * 8.0f;
			}

			weapon.DestroyGameObject();
		}

		_ = FinishDropAsync();

		return true;
	}

	private async Task FinishDropAsync()
	{
		await Task.Yield();
		var best = GetBestWeapon();
		if ( best.IsValid() )
		{
			SwitchWeapon( best );
		}

		SaveLoadout();
	}

	/// <summary>
	/// Sound played on the local client when picking up a new weapon (set in inspector).
	/// </summary>
	[Property] public SoundEvent PickupSound { get; set; }

	[Rpc.Owner]
	private void OnClientPickup( BaseCarryable weapon, bool justAmmo = false )
	{
		if ( !weapon.IsValid() ) return;

		if ( ShouldAutoswitchTo( weapon ) )
		{
			SwitchWeapon( weapon );
		}

		if ( Player.IsLocalPlayer )
		{
			ILocalPlayerEvent.Post( e => e.OnPickup( weapon ) );

			// New: feedback sound for picked-up weapons (skips ammo-only pickups).
			if ( !justAmmo && PickupSound.IsValid() )
				Sound.Play( PickupSound );
		}
	}

	private bool ShouldAutoswitchTo( BaseCarryable item )
	{
		Assert.True( item.IsValid(), "item invalid" );

		if ( !ActiveWeapon.IsValid() )
			return true;

		if ( !GamePreferences.AutoSwitch )
			return false;

		if ( ActiveWeapon.IsInUse() )
			return false;

		if ( item is BaseWeapon weapon && weapon.UsesAmmo )
		{
			if ( !weapon.HasAmmo() && !weapon.CanReload() )
			{
				return false;
			}
		}

		return item.Value > ActiveWeapon.Value;
	}

	/// <summary>
	/// Moves the item in <paramref name="fromSlot"/> to <paramref name="toSlot"/>.
	/// If both slots are occupied the items are swapped; if <paramref name="toSlot"/> is
	/// empty the item is simply relocated.
	/// </summary>
	public void MoveSlot( int fromSlot, int toSlot )
	{
		if ( !Networking.IsHost )
		{
			HostMoveSlot( fromSlot, toSlot );
			return;
		}

		if ( fromSlot == toSlot ) return;
		if ( fromSlot < 0 || fromSlot >= MaxSlots ) return;
		if ( toSlot < 0 || toSlot >= MaxSlots ) return;

		var fromWeapon = GetSlot( fromSlot );
		if ( !fromWeapon.IsValid() ) return;

		var toWeapon = GetSlot( toSlot );

		fromWeapon.InventorySlot = toSlot;
		if ( toWeapon.IsValid() )
			toWeapon.InventorySlot = fromSlot;

		SaveLoadout();
	}

	[Rpc.Host]
	private void HostMoveSlot( int fromSlot, int toSlot )
	{
		MoveSlot( fromSlot, toSlot );
	}

	public BaseCarryable GetBestWeapon()
	{
		return Weapons.OrderByDescending( x => x.Value ).FirstOrDefault();
	}

	public void SwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		if ( !Networking.IsHost )
		{
			HostSwitchWeapon( weapon, allowHolster );
			return;
		}

		if ( weapon == ActiveWeapon )
		{
			if ( allowHolster )
			{
				ActiveWeapon = null;
			}
			return;
		}

		ActiveWeapon = weapon;
	}

	[Rpc.Host]
	private void HostSwitchWeapon( BaseCarryable weapon, bool allowHolster = false )
	{
		SwitchWeapon( weapon, allowHolster );
	}

	protected override void OnUpdate()
	{
		var renderer = Player?.Controller?.Renderer;

		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnFrameUpdate( Player );

			if ( renderer.IsValid() )
			{
				renderer.Set( "holdtype", (int)ActiveWeapon.HoldType );
			}
		}
		else
		{
			if ( renderer.IsValid() )
			{
				renderer.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.None );
			}
		}
	}

	public void OnControl()
	{
		if ( Input.Pressed( "drop" ) )
		{
			if ( ActiveWeapon.IsValid() )
				DropActiveWeapon();

			return;
		}

		if ( ActiveWeapon.IsValid() && !ActiveWeapon.IsProxy )
			ActiveWeapon.OnPlayerUpdate( Player );
	}

	/// <summary>
	/// Called by the owning client to drop their currently held weapon.
	/// </summary>
	[Rpc.Host]
	private void DropActiveWeapon()
	{
		if ( !ActiveWeapon.IsValid() ) return;
		Drop( ActiveWeapon );
	}

	[Rpc.Host]
	private void HostDrop( BaseCarryable weapon )
	{
		Drop( weapon );
	}

	/// <summary>
	/// Removes a weapon from the inventory and destroys it without dropping it into the world.
	/// </summary>
	public void Remove( BaseCarryable weapon )
	{
		if ( !Networking.IsHost )
		{
			HostRemove( weapon );
			return;
		}
		_ = RemoveAsync( weapon );
	}

	private async Task RemoveAsync( BaseCarryable weapon )
	{
		if ( !weapon.IsValid() ) return;
		if ( weapon.Owner != Player ) return;

		if ( ActiveWeapon == weapon )
			SwitchWeapon( null, true );

		weapon.DestroyGameObject();
		await Task.Yield(); // wait for GameObject to be destroyed

		var best = GetBestWeapon();
		if ( best.IsValid() )
			SwitchWeapon( best );

		SaveLoadout();
	}

	[Rpc.Host]
	private void HostRemove( BaseCarryable weapon )
	{
		Remove( weapon );
	}

	private bool _isRestoringLoadout;

	public struct SavedPreset
	{
		public string Name { get; set; }
		public string LoadoutJson { get; set; }
	}

	public static IReadOnlyList<SavedPreset> GetLoadoutPresets()
	{
		return LocalData.Get<List<SavedPreset>>( "presets", new() );
	}

	/// <summary>
	/// Saves (or overwrites) a named preset entry with the given loadout.
	/// </summary>
	public static void SaveLoadoutPreset( string name, string loadoutJson )
	{
		var presets = LocalData.Get<List<SavedPreset>>( "presets", new() );
		var idx = presets.FindIndex( p => p.Name == name );
		var entry = new SavedPreset { Name = name, LoadoutJson = loadoutJson };
		if ( idx >= 0 )
			presets[idx] = entry;
		else
			presets.Add( entry );
		LocalData.Set( "presets", presets );
	}

	/// <summary>
	/// Removes a preset if it exists.
	/// </summary>
	public static void DeleteLoadoutPreset( string name )
	{
		var presets = LocalData.Get<List<SavedPreset>>( "presets", new() );
		presets.RemoveAll( p => p.Name == name );
		LocalData.Set( "presets", presets );
	}

	/// <summary>
	/// Clears the inventory and restores it from the given JSON.
	/// </summary>
	public void SwitchToPreset( string loadoutJson )
	{
		if ( !Networking.IsHost )
		{
			HostSwitchToPreset( loadoutJson );
			return;
		}
		_ = SwitchToPresetAsync( loadoutJson );
	}

	/// <summary>
	/// Clears the inventory and restores the default weapons (physgun, toolgun, camera).
	/// </summary>
	public void ResetToDefault()
	{
		if ( !Networking.IsHost )
		{
			HostResetToDefault();
			return;
		}
		_ = ResetToDefaultAsync();
	}

	[Rpc.Host]
	private void HostResetToDefault()
	{
		_ = ResetToDefaultAsync();
	}

	private async Task ResetToDefaultAsync()
	{
		foreach ( var weapon in Weapons.ToList() )
			weapon.DestroyGameObject();

		await Task.Yield();

		GiveDefaultWeapons();
		SwitchWeapon( GetBestWeapon() );
		SaveLoadout();
	}

	[Rpc.Host]
	private void HostSwitchToPreset( string loadoutJson )
	{
		_ = SwitchToPresetAsync( loadoutJson );
	}

	private async Task SwitchToPresetAsync( string loadoutJson )
	{
		var previousSlot = ActiveWeapon?.InventorySlot ?? 0;

		foreach ( var weapon in Weapons.ToList() )
			weapon.DestroyGameObject();

		// Yield so we can process everything queued for deletion first (so we don't delete anything we are about to add)
		await Task.Yield();

		await EnsureMountedAsync( loadoutJson );
		GiveLoadoutWeapons( loadoutJson );

		// Re-equip whichever slot the player was holding
		var toEquip = GetSlot( previousSlot ) ?? GetBestWeapon();
		if ( toEquip.IsValid() )
			SwitchWeapon( toEquip );

		SaveLoadout();
	}


	/// <summary>
	/// One entry in a serialized loadout: the prefab resource path and the slot it occupies.
	/// </summary>
	private struct LoadoutEntry
	{
		public string PrefabPath { get; set; }
		public int Slot { get; set; }
		public string SpawnerDataPayload { get; set; }
	}

	private string SerializeLoadout()
	{
		var entries = Weapons
			.Where( w => !string.IsNullOrEmpty( w.GameObject.PrefabInstanceSource ) )
			.Select( w => new LoadoutEntry
			{
				PrefabPath = w.GameObject.PrefabInstanceSource,
				Slot = w.InventorySlot,
				// Preserve the spawner-specific payload (prop path, entity path, dupe JSON, etc.)
				SpawnerDataPayload = (w as SpawnerWeapon)?.SpawnerData
			} )
			.ToList();

		return entries.Count > 0 ? Json.Serialize( entries ) : null;
	}

	/// <summary>
	/// Saves the current loadout/hotbar.
	/// </summary>
	public void SaveLoadout()
	{
		if ( _isRestoringLoadout ) return; var json = SerializeLoadout();
		if ( string.IsNullOrEmpty( json ) ) return;

		if ( Player.IsLocalPlayer )
		{
			LocalData.Set( "hotbar", json );
		}
		else
		{
			PushLoadoutToClient( json );
		}
	}

	[Rpc.Owner]
	private void PushLoadoutToClient( string loadoutJson )
	{
		LocalData.Set( "hotbar", loadoutJson );
	}

	[Rpc.Owner]
	private void RequestClientLoadout()
	{
		var json = LocalData.Get<string>( "hotbar" );
		if ( !string.IsNullOrEmpty( json ) )
			RestoreLoadoutFromClient( json );
	}

	private static async Task EnsureMountedAsync( string json )
	{
		var entries = Json.Deserialize<List<LoadoutEntry>>( json );
		if ( entries is null ) return;

		var needsMounts = entries.Any( e => !string.IsNullOrEmpty( e.SpawnerDataPayload )
			&& e.SpawnerDataPayload.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) );

		if ( !needsMounts ) return;

		foreach ( var entry in Sandbox.Mounting.Directory.GetAll().Where( e => e.Available ) )
			await Sandbox.Mounting.Directory.Mount( entry.Ident );
	}

	[Rpc.Host]
	private async void RestoreLoadoutFromClient( string loadoutJson )
	{
		foreach ( var weapon in Weapons.ToList() )
			weapon.DestroyGameObject();

		// Same as SwitchToPresetAsync, let deletions settle before restoring.
		await Task.Yield();

		await EnsureMountedAsync( loadoutJson );
		GiveLoadoutWeapons( loadoutJson );

		// Switch to the best weapon after restoring the loadout.
		var best = GetBestWeapon();
		if ( best.IsValid() )
			SwitchWeapon( best );
	}

	private void GiveLoadoutWeapons( string json )
	{
		var entries = Json.Deserialize<List<LoadoutEntry>>( json );
		if ( entries is null ) return;

		_isRestoringLoadout = true;
		try
		{
			foreach ( var entry in entries )
			{
				if ( !Pickup( entry.PrefabPath, entry.Slot, false ) )
					continue;

				// If this slot held a configured spawner, restore its payload.
				if ( !string.IsNullOrEmpty( entry.SpawnerDataPayload ) && GetSlot( entry.Slot ) is SpawnerWeapon spawnerWeapon )
				{
					spawnerWeapon.RestoreSpawnerData( entry.SpawnerDataPayload );
				}
			}
		}
		finally
		{
			_isRestoringLoadout = false;
		}
	}

	void IPlayerEvent.OnSpawned()
	{
		_ = OnSpawnedAsync();
	}

	private async Task OnSpawnedAsync()
	{
		if ( Player.IsLocalPlayer )
		{
			var json = LocalData.Get<string>( "hotbar" );
			if ( !string.IsNullOrEmpty( json ) )
			{
				await EnsureMountedAsync( json );
				GiveLoadoutWeapons( json );
				return;
			}
		}
		else
		{
			RequestClientLoadout();
		}

		GiveDefaultWeapons();
	}

	void IPlayerEvent.OnDied( IPlayerEvent.DiedParams args )
	{
		if ( ActiveWeapon.IsValid() )
		{
			ActiveWeapon.OnPlayerDeath( args );
		}

		// Only the host has the full weapon list with SourcePrefabPath populated.
		if ( Networking.IsHost )
		{
			SaveLoadout();
		}
	}

	void IPlayerEvent.OnCameraMove( ref Angles angles )
	{
		if ( !ActiveWeapon.IsValid() ) return;

		ActiveWeapon.OnCameraMove( Player, ref angles );
	}

	void IPlayerEvent.OnCameraPostSetup( Sandbox.CameraComponent camera )
	{
		if ( !ActiveWeapon.IsValid() ) return;

		ActiveWeapon.OnCameraSetup( Player, camera );
	}

	void ISaveEvents.BeforeSave( string filename )
	{
		if ( !Networking.IsHost ) return;

		var steamId = Player.SteamId;
		if ( steamId == 0 ) return;

		var json = SerializeLoadout();
		if ( string.IsNullOrEmpty( json ) ) return;

		// Store the hotbar loadout in save's metadata
		SaveSystem.Current?.SetMetadata( $"Loadout_{steamId}", json );
	}

	void ISaveEvents.AfterLoad( string filename )
	{
		if ( !Networking.IsHost ) return;

		var steamId = Player.SteamId;
		if ( steamId == 0 ) return;

		// Restore the hotbar loadout
		var json = SaveSystem.Current?.GetMetadata( $"Loadout_{steamId}" );
		if ( string.IsNullOrEmpty( json ) ) return;

		_ = RestoreLoadoutFromSaveAsync( json );
	}

	private async Task RestoreLoadoutFromSaveAsync( string json )
	{
		foreach ( var weapon in Weapons.ToList() )
			weapon.DestroyGameObject();

		// Let queued destructions finish before adding new weapons.
		await Task.Yield();

		await EnsureMountedAsync( json );
		GiveLoadoutWeapons( json );

		var best = GetBestWeapon();
		if ( best.IsValid() )
			SwitchWeapon( best );
	}
}
```

## Ключевые паттерны

### Host Authority (авторитет хоста)

Все мутации инвентаря проходят через хост. Клиент вызывает `[Rpc.Host]` метод (например `HostSwitchWeapon`), хост применяет изменение, `[Sync]` автоматически рассылает обновлённое состояние.

### async/await и Task.Yield

```csharp
weapon.DestroyGameObject();
await Task.Yield();           // ← ждём один кадр
GiveLoadoutWeapons( json );   // теперь safe
```

`DestroyGameObject()` не удаляет объект мгновенно — он помечается на удаление и уничтожается в конце кадра. `Task.Yield()` ждёт один кадр, чтобы удаление завершилось. Без этого новое оружие могло бы конфликтовать с ещё не удалённым старым.

### Mounting

```csharp
await Sandbox.Mounting.Directory.Mount( entry.Ident );
```

Если loadout содержит оружие из «монтированных» игр (HL2, CS:GO модели), нужно сначала подключить эти ресурсы. `EnsureMountedAsync` проверяет и подключает нужные пакеты.

## Проверка

1. При спавне получаешь physgun + toolgun → проверь 2 слота
2. Подбери оружие → появляется в инвентаре
3. Нажми G → выбрасываешь оружие
4. Умри и респавнись → loadout восстановлен

## Следующий файл

Переходи к **03.14 — Наблюдатель (PlayerObserver)**.

---

<!-- seealso -->
## 🔗 См. также

- [06.01 — BaseCarryable](06_01_BaseCarryable.md)
- [06.04 — BaseWeapon](06_04_BaseWeapon.md)
- [08.01 — Physgun](08_01_Physgun.md)
- [09.06 — Toolgun](09_06_Toolgun.md)
- [23.01 — ISaveEvents](23_01_ISaveEvents.md)
- [23.02 — SaveSystem](23_02_SaveSystem.md)

