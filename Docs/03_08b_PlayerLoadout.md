# 03.08b — Loadout игрока (PlayerLoadout) 🎒💾

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [01.06 — LocalData](01_06_LocalData.md)
>
> **🔗 Связанные документы:**
>
> - [03.08 — PlayerInventory](03_08_PlayerInventory.md) — собственно слоты и подбор/выброс
> - [23.01 — Global.ISaveEvents](23_01_ISaveEvents.md) — события системы сохранений
> - [23.02 — SaveSystem](23_02_SaveSystem.md) — куда пишутся метаданные

## Что мы делаем?

Добавляем **`PlayerLoadout`** — компонент, который живёт на том же `GameObject` игрока, что и `PlayerInventory`, и отвечает за:

- Сериализацию набора оружия (loadout) в JSON.
- **Автосохранение** при изменении инвентаря (подобрал, выбросил, удалил, перенёс слот, умер).
- Восстановление набора при респавне (с диска, у клиента или дефолтный).
- Сохранение/загрузку именованных **пресетов** (`SavedPreset`) в `LocalData`.
- Сохранение набора в save-файл карты через `Global.ISaveEvents` (по `SteamId`).

Раньше всё это жило прямо в `PlayerInventory`, но в актуальной версии sandbox логика выделена в отдельный компонент — это упростило `PlayerInventory` и сделало loadout-систему независимо тестируемой.

## Архитектура

```
GameObject игрока
├── Player                (модель данных игрока + IDamageable + Global.ISaveEvents)
├── PlayerInventory       (слоты, ActiveWeapon, Pickup/Drop/Switch/Move/Remove)
└── PlayerLoadout         (Save/Restore/Preset + Global.ISaveEvents) ← этот файл
```

`PlayerLoadout` объявлен как:

```csharp
public sealed class PlayerLoadout : Component,
    Local.IPlayerEvents,    // слушает события инвентаря «своего» игрока
    Global.IPlayerEvents,   // слушает OnPlayerSpawned (для всех игроков, фильтрует по this)
    Global.ISaveEvents      // сохраняет/загружает loadout в сейв карты
{
    [RequireComponent] public Player Player { get; set; }
    [RequireComponent] public PlayerInventory Inventory { get; set; }
    ...
}
```

`[RequireComponent]` гарантирует, что `Player` и `PlayerInventory` уже существуют на том же `GameObject` — иначе компонент не добавится. См. [00.13 — Атрибуты компонентов](00_13_Component_Attributes.md).

## Структуры данных

### `LoadoutEntry`

```csharp
public struct LoadoutEntry
{
    public string PrefabPath { get; set; }          // путь к префабу оружия
    public int Slot { get; set; }                   // целевой слот (0..MaxSlots-1)
    public string SpawnerDataPayload { get; set; }  // payload для SpawnerWeapon (модель пропа и т.п.)
}
```

Один элемент сериализованного набора. Список таких элементов сохраняется как JSON-строка.

### `SavedPreset`

```csharp
public struct SavedPreset
{
    public string Name { get; set; }
    public string LoadoutJson { get; set; }
}
```

Именованный пресет — пара «имя + JSON-loadout». Все пресеты лежат в `LocalData` под ключом `"presets"`.

## Сериализация

```csharp
public string SerializeLoadout()
{
    var entries = Inventory.Weapons
        .Where( w => !string.IsNullOrEmpty( w.GameObject.PrefabInstanceSource ) )
        .Select( w => new LoadoutEntry
        {
            PrefabPath = w.GameObject.PrefabInstanceSource,
            Slot = w.InventorySlot,
            SpawnerDataPayload = (w as SpawnerWeapon)?.SpawnerData
        } )
        .ToList();

    return entries.Count > 0 ? Json.Serialize( entries ) : null;
}
```

- Пропускаются «оружия» без `PrefabInstanceSource` — их нельзя восстановить из префаба.
- Для `SpawnerWeapon` (универсальное «оружие-спавнер» из меню спавна) дополнительно сохраняется его внутренний `SpawnerData`, чтобы на следующий запуск пользователь получил тот же выбранный для спавна предмет.
- Возвращает `null`, если выдавать нечего, — чтобы не перезатереть существующий сейв пустотой.

## Сохранение и хранилище

```csharp
public void SaveLoadout()
{
    if ( _isRestoringLoadout ) return;

    var json = SerializeLoadout();
    if ( string.IsNullOrEmpty( json ) ) return;

    if ( Player.IsLocalPlayer )
        LocalData.Set( "hotbar", json );           // локально на диск
    else
        PushLoadoutToClient( json );                // RPC.Owner → клиент сохраняет у себя
}
```

| Кто игрок | Куда пишем |
|---|---|
| Локальный | `LocalData.Set("hotbar", json)` (см. [01.06 — LocalData](01_06_LocalData.md)) |
| Удалённый | RPC `PushLoadoutToClient` → клиент пишет у себя в `LocalData` |
| Сохранение карты | `SaveSystem.SetMetadata( $"Loadout_{steamId}", json )` (только хост) |

`_isRestoringLoadout` — защита от рекурсии: пока мы выдаём оружие из загруженного loadout'а, события `OnPickup` тоже стреляют, но мы не хотим тут же заново сериализовать неполный набор.

## Восстановление при респавне

`PlayerLoadout` слушает `Global.IPlayerEvents.OnPlayerSpawned` и фильтрует «свой» спавн:

```csharp
void Global.IPlayerEvents.OnPlayerSpawned( Player player )
{
    if ( player != Player ) return;
    if ( !Networking.IsHost ) return;

    _ = RestoreOnSpawnAsync();
}
```

Алгоритм восстановления (`RestoreOnSpawnAsync`):

1. **Локальный игрок** → читаем JSON из `LocalData["hotbar"]`. Если есть — выдаём оружие, переключаемся на лучшее.
2. **Удалённый игрок** → шлём `RequestClientLoadout` ([Rpc.Owner]); клиент возвращает свой JSON через `HostRestoreLoadoutFromClient` ([Rpc.Host]).
3. **Если JSON пустой** → `Inventory.GiveDefaultWeapons()` (physgun + toolgun + camera).

### `EnsureMountedAsync` — догрузка маунтов

Если в loadout'е есть `SpawnerWeapon` с моделью из workshop-маунта (`*.vmdl`), перед выдачей оружия мы прокручиваем все доступные маунты и вызываем `Sandbox.Mounting.Directory.Mount(...)`. Иначе при попытке заклонить префаб модель ещё не будет распакована, и оружие восстановится «пустым».

## Пресеты

Пресеты — `static`-API, потому что хранилище общее для всех игроков на этом клиенте:

```csharp
public static IReadOnlyList<SavedPreset> GetLoadoutPresets();
public static void SaveLoadoutPreset( string name, string loadoutJson );
public static void DeleteLoadoutPreset( string name );
```

Сами действия игрока с пресетами идут как RPC, чтобы всегда исполнялись на хосте (он владеет инвентарём):

```csharp
public void SwitchToPreset( string loadoutJson );  // → HostSwitchToPreset (Rpc.Host)
public void ResetToDefault();                      // → HostResetToDefault  (Rpc.Host)
```

`SwitchToPresetAsync`:

1. Запоминает активный слот.
2. Уничтожает все текущие оружия инвентаря.
3. `await Task.Yield()` — даёт сцене один тик на чистку.
4. `EnsureMountedAsync` + `GiveLoadoutWeapons` — выдаёт оружие из preset-JSON.
5. Переключается на оружие в том же слоте, что было активно (или на лучшее).
6. `SaveLoadout()` — фиксирует выбор как новый «активный» loadout.

`ResetToDefaultAsync` — то же самое, но без preset-JSON: сразу `Inventory.GiveDefaultWeapons()`.

## Автосохранение через `Local.IPlayerEvents`

```csharp
void Local.IPlayerEvents.OnDied( PlayerDiedParams args )         => SaveLoadout();      // host
void Local.IPlayerEvents.OnPickup( PlayerPickupEvent e )         => SaveLoadout();      // host
void Local.IPlayerEvents.OnDrop( PlayerDropEvent e )             => SaveLoadoutAfterYield();
void Local.IPlayerEvents.OnRemoveWeapon( PlayerRemoveWeaponEvent e ) => SaveLoadoutAfterYield();
void Local.IPlayerEvents.OnMoveSlot( PlayerMoveSlotEvent e )     => SaveLoadout();      // host
```

- `Drop` и `Remove` сохраняются **через `Task.Yield()`** — потому что в этот момент оружие ещё не успело отвалиться от иерархии, и `Inventory.Weapons` всё ещё содержит его. Подождать тик — и сериализация увидит уже актуальное состояние.
- `Cancelled`-события игнорируются (`if ( e.Cancelled ) return;`).
- Все обработчики проверяют `Networking.IsHost` — клиенты не пишут свой loadout по этим событиям, они получат RPC от хоста.

## Сохранение в save-файл карты

```csharp
void Global.ISaveEvents.BeforeSave( string filename )
{
    if ( !Networking.IsHost ) return;
    var steamId = Player.SteamId;
    if ( steamId == 0 ) return;

    var json = SerializeLoadout();
    if ( string.IsNullOrEmpty( json ) ) return;

    SaveSystem.Current?.SetMetadata( $"Loadout_{steamId}", json );
}

void Global.ISaveEvents.AfterLoad( string filename )
{
    if ( !Networking.IsHost ) return;
    var steamId = Player.SteamId;
    if ( steamId == 0 ) return;

    var json = SaveSystem.Current?.GetMetadata( $"Loadout_{steamId}" );
    if ( string.IsNullOrEmpty( json ) ) return;

    _ = RestoreLoadoutFromSaveAsync( json );
}
```

В save-файле каждый игрок хранит свой loadout под ключом `Loadout_{steamId}`. Это позволяет, перезагрузив сейв через сутки, получить ровно тот набор оружия, с которым его делали — даже если игрок перед сохранением успел сменить пресет.

## Полный код

См. [`Code/Player/PlayerLoadout.cs`](../Code/Player/PlayerLoadout.cs) — на момент написания файл содержит ~330 строк и соответствует приведённому выше описанию (структуры, методы, RPC, обработчики событий).

## Что проверить

1. Подбери оружие, перезайди в игру — должен восстановиться твой набор.
2. На втором клиенте подбери оружие, кикни этого игрока, реконнект — набор должен прийти с клиента (через `RequestClientLoadout`).
3. Сохрани игру (`save mygame`), кикни всех, загрузи — у каждого `SteamId` восстановится свой loadout из метаданных сейва.
4. Создай пресет, выйди и зайди в другую сессию — пресет должен остаться в `LocalData["presets"]`.
5. Удали пресет — он должен исчезнуть и не появиться при следующем запуске.
6. Возьми оружие из workshop-маунта в пресет, размонтируй маунт, восстанови пресет — `EnsureMountedAsync` должен поднять маунт автоматически перед выдачей.

---

## ➡️ Следующий шаг

Переходи к **[03.09 — PlayerFlashlight](03_09_PlayerFlashlight.md)**.
