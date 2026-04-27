# 04.01 — Игровой менеджер (GameManager) 🎮

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.25 — События сети](00_25_Network_Events.md)

## Что мы делаем?

Создаём **GameManager** — центральную систему управления игрой. Это `GameObjectSystem` (синглтон), который:
- Создаёт сетевое лобби при старте
- Обрабатывает подключение/отключение игроков
- Спавнит игроков на спавн-поинтах
- Обрабатывает смерти и ленту убийств
- Управляет спавном объектов (пропы, сущности, дупликаты) — через `ISpawnEvents`
- Предоставляет RPC для удалённого изменения свойств

> 📁 **Класс `partial`-ный.** В исходниках он разнесён по двум файлам:
> - **`Code/GameLoop/GameManager.cs`** — лобби, подключения, спавн игроков, смерти, общие RPC (`ChangeProperty`, `DeleteInspectedObject`, `BreakInspectedProp`, морфы, материалы, выдача `SpawnerWeapon`).
> - **`Code/GameLoop/GameManager.Spawn.cs`** — всё, что связано со спавном объектов через `ISpawner` (консольная команда `spawn`, парсинг `ident`, `FindDupe`, `SpawnAndUndo` с диспатчем [`ISpawnEvents`](14_01b_ISpawnEvents.md)).
>
> Раньше эти куски лежали в одном файле; разделение упростило подключение событий и поддержку.

## Архитектура

```
GameManager : GameObjectSystem<GameManager>
  ├── INetworkListener      — подключение/отключение
  ├── ISceneStartup         — инициализация хоста
  ├── IScenePhysicsEvents   — обработка out-of-bounds
  ├── ICleanupEvents        — уведомления об очистке
  └── ISaveEvents           — загрузка сохранений
```

### Жизненный цикл подключения

```
Игрок подключается
  → OnActive(Connection)
    → CreatePlayerInfo()    // создать PlayerData
    → SpawnPlayer()         // создать Player
    → CheckAchievement()   // проверить ачивки
    → Chat: "X has joined" // уведомление в чат

Игрок отключается
  → OnDisconnected(Connection)
    → PlayerData.Destroy()
    → Chat: "X has left"
```

### Спавн игрока

```csharp
public void SpawnPlayer( PlayerData playerData )
{
    // 1. Проверяем: уже есть Player для этого подключения?
    if ( Scene.GetAll<Player>().Any( x => x.Network.Owner?.Id == playerData.PlayerId ) )
        return;

    // 2. Ищем спавн-поинт (случайный)
    var startLocation = FindSpawnLocation().WithScale( 1 );

    // 3. Клонируем префаб игрока
    var playerGo = GameObject.Clone( "/prefabs/engine/player.prefab", ... );

    // 4. Назначаем PlayerData
    player.PlayerData = playerData;

    // 5. Создаём в сети (владелец = подключение)
    playerGo.NetworkSpawn( owner );

    // 6. Вызываем OnSpawned (даёт оружие)
    IPlayerEvent.PostToGameObject( player.GameObject, x => x.OnSpawned() );
}
```

### Обработка смерти (OnDeath)

```csharp
public void OnDeath( Player player, DamageInfo dmg )
{
    var source = dmg.Attacker?.GetComponent<IKillSource>();
    source.OnKill( player.GameObject );    // +1 kill
    player.PlayerData.Deaths++;            // +1 death
    
    // Лента убийств
    Scene.RunEvent<Feed>( x => x.NotifyKill(...) );
}
```

### Спавн объектов (Spawn) — файл `GameManager.Spawn.cs`

Универсальная система спавна по строковому идентификатору:

```
"prop:models/citizen_props/crate01.vmdl"  → PropSpawner
"entity:light_point"                       → EntitySpawner
"mount:hl2/models/..."                     → MountSpawner
"dupe:12345"                               → DuplicatorSpawner
```

Алгоритм:
1. Игрок вызывает `Spawn(ident)` (RPC `Rpc.Broadcast`).
2. На клиенте-инициаторе: звук + статистика.
3. На хосте: трейс от глаз → определяем позицию.
4. Парсим тип через `SpawnlistItem.ParseIdent` и создаём соответствующий `ISpawner`.
5. Передаём в **`SpawnAndUndo(spawner, transform, player)`**, который:
   - стреляет [`ISpawnEvents.OnSpawn`](14_01b_ISpawnEvents.md) — слушатели (например, [`LimitsSystem`](04_06_LimitsSystem.md), античит, зональные блокировки) могут отменить спавн через `data.Cancelled = true`;
   - если не отменено — вызывает `await spawner.Spawn(...)`;
   - регистрирует созданные объекты в Undo-стеке игрока;
   - стреляет `ISpawnEvents.OnPostSpawn` со списком созданных объектов.

Тот же протокол использует [`SpawnerWeapon.DoSpawn`](14_06_SpawnerWeapon.md) — поэтому слушателю достаточно реализовать `ISpawnEvents` один раз, и он покроет оба источника спавна.

## Создай файл

Путь: `Code/GameLoop/GameManager.cs` (основная логика) + `Code/GameLoop/GameManager.Spawn.cs` (универсальный спавн через `ISpawner`/`ISpawnEvents`).

*(Полный код — см. исходники в репозитории.)*

Ключевые секции файла:

```csharp
public sealed partial class GameManager : GameObjectSystem<GameManager>, 
    Component.INetworkListener, ISceneStartup, IScenePhysicsEvents, ICleanupEvents, ISaveEvents
{
    // Конструктор GameObjectSystem
    public GameManager( Scene scene ) : base( scene ) { }

    // При старте хоста — создаём лобби
    void ISceneStartup.OnHostInitialize() { ... }

    // При подключении — создаём PlayerData + спавним Player
    void Component.INetworkListener.OnActive( Connection channel ) { ... }

    // При отключении — удаляем PlayerData
    void Component.INetworkListener.OnDisconnected( Connection channel ) { ... }

    // Спавн игрока на спавн-поинте
    public void SpawnPlayer( PlayerData playerData ) { ... }

    // Обработка смерти — лента убийств
    public void OnDeath( Player player, DamageInfo dmg ) { ... }

    // Универсальный спавн по идентификатору
    [Rpc.Broadcast]
    public static async void Spawn( string ident, string metadata = null ) { ... }

    // Изменение свойства компонента по сети
    [Rpc.Host]
    public static void ChangeProperty( Component c, string propertyName, object value ) { ... }

    // Объекты, вылетевшие за границы карты, уничтожаются
    void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body ) { body.DestroyGameObject(); }
}
```

## Ключевые концепции

### GameObjectSystem vs Component

`GameObjectSystem<T>` — синглтон, привязанный к сцене (не к конкретному GameObject). Доступ: `GameManager.Current`. Идеален для глобальных систем.

### INetworkListener

Интерфейс движка: `OnActive` (подключение) и `OnDisconnected` (отключение). Вызывается **только на хосте**.

### CanSpawnObjects

```csharp
channel.CanSpawnObjects = false;
```

Запрещаем клиенту самостоятельно создавать сетевые объекты. Все создания проходят через хост (античит).

### SpawnlistItem.ParseIdent

```csharp
var (type, path, source) = SpawnlistItem.ParseIdent( ident );
// "prop:models/box.vmdl" → ("prop", "models/box.vmdl", null)
// "dupe:12345"           → ("dupe", "12345", "local"|"workshop")
```

## Проверка

1. Запусти сервер → лобби создаётся автоматически
2. Подключи второго игрока → появляется на спавн-поинте
3. Убей игрока → лента убийств показывает «X killed Y»
4. Консоль: `spawn prop:models/citizen_props/crate01.vmdl` → проп спавнится

## Следующий файл

Переходи к **04.02 — Утилиты GameManager (GameManager.Util)**.

---

<!-- seealso -->
## 🔗 См. также

- [04.06 — LimitsSystem](04_06_LimitsSystem.md) — серверные ConVar-лимиты, использует `ISpawnEvents` из `GameManager.Spawn.cs`
- [14.01b — ISpawnEvents](14_01b_ISpawnEvents.md) — события спавна, которые стреляет `SpawnAndUndo`
- [23.01 — ISaveEvents](23_01_ISaveEvents.md)
- [02.01 — IKillSource](02_01_IKillSource.md)
- [14.01 — ISpawner](14_01_ISpawner.md)

