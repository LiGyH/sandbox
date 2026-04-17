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
- Управляет спавном объектов (пропы, сущности, дупликаты)
- Предоставляет RPC для удалённого изменения свойств

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

### Спавн объектов (Spawn)

Универсальная система спавна по строковому идентификатору:

```
"prop:models/citizen_props/crate01.vmdl"  → PropSpawner
"entity:light_point"                       → EntitySpawner
"mount:hl2/models/..."                     → MountSpawner
"dupe:12345"                               → DuplicatorSpawner
```

Алгоритм:
1. Игрок вызывает `Spawn(ident)` (RPC)
2. На клиенте: звук + статистика
3. На хосте: трейс от глаз → определяем позицию
4. Парсим тип и создаём соответствующий `ISpawner`
5. Спавним и регистрируем в Undo-стеке

## Создай файл

Путь: `Code/GameLoop/GameManager.cs`

*(Полный код — 300+ строк — см. исходник в `Code/GameLoop/GameManager.cs`)*

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
