# Этап 3: GameManager — сердце игры

## Цель этого этапа

Создать GameManager — центральную систему, которая:
- Создаёт игрока, когда кто-то подключается к серверу
- Находит точку спавна на карте
- Управляет респавном после смерти
- Обрабатывает отключение игроков

После этого этапа ты сможешь **зайти в игру и ходить по карте!**

## Что такое GameManager?

GameManager — это **GameObjectSystem**. Это не обычный компонент — это глобальная система, которая существует в единственном экземпляре на всю сцену.

### Разница между Component и GameObjectSystem:

```
Component (обычный компонент):
- Прикреплён к конкретному GameObject
- Может быть несколько экземпляров
- Пример: Player, PlayerFlashlight

GameObjectSystem (глобальная система):
- Существует в ЕДИНСТВЕННОМ экземпляре на всю сцену
- НЕ прикреплён к GameObject — он "плавает" в сцене
- Доступ через: GameManager.Current
- Пример: GameManager, CleanupSystem
```

**В движке** (`sbox-public`): `GameObjectSystem<T>` — это generic-класс. Он автоматически создаётся при загрузке сцены и доступен через статическое свойство `T.Current`. Движок гарантирует, что будет только один экземпляр.

## Шаг 3.1: Понимаем интерфейсы GameManager

Наш GameManager реализует несколько **интерфейсов** — контрактов, которые говорят движку, на какие события мы хотим реагировать:

```csharp
public class GameManager : GameObjectSystem<GameManager>,
    Component.INetworkListener,    // Сетевые события (подключение/отключение)
    ISceneStartup,                 // Запуск сцены
    IScenePhysicsEvents            // Физические события
```

| Интерфейс | Что даёт |
|-----------|---------|
| `Component.INetworkListener` | `OnActive()` — игрок подключился, `OnDisconnected()` — отключился |
| `ISceneStartup` | `OnHostInitialize()` — сцена запускается как хост |
| `IScenePhysicsEvents` | `OnOutOfBounds()` — объект вылетел за пределы карты |

## Шаг 3.2: Создаём GameManager

Создай файл `Code/GameLoop/GameManager.cs`:

```csharp
/// <summary>
/// Главный менеджер игры. Управляет подключением игроков, спавном, смертью.
/// Это GameObjectSystem — существует в единственном экземпляре.
/// </summary>
public sealed partial class GameManager : GameObjectSystem<GameManager>, 
    Component.INetworkListener, 
    ISceneStartup
{
    /// <summary>
    /// Конструктор — вызывается движком при создании сцены.
    /// </summary>
    public GameManager( Scene scene ) : base( scene )
    {
    }

    /// <summary>
    /// Вызывается когда сцена инициализируется как хост (сервер).
    /// Здесь мы создаём лобби для мультиплеера.
    /// </summary>
    void ISceneStartup.OnHostInitialize()
    {
        if ( !Networking.IsActive )
        {
            Networking.CreateLobby( new Sandbox.Network.LobbyConfig() 
            { 
                Privacy = Sandbox.Network.LobbyPrivacy.Public, 
                MaxPlayers = 32, 
                Name = "Sandbox", 
                DestroyWhenHostLeaves = true 
            });
        }
    }

    /// <summary>
    /// Вызывается когда игрок ПОДКЛЮЧАЕТСЯ к серверу.
    /// Это первый метод, который вызывается для нового игрока.
    /// </summary>
    void Component.INetworkListener.OnActive( Connection channel )
    {
        // Запрещаем клиенту создавать объекты напрямую (безопасность)
        channel.CanSpawnObjects = false;

        // Создаём данные игрока (kills, deaths, имя)
        var playerData = CreatePlayerInfo( channel );
        
        // Спавним игрока на карте
        SpawnPlayer( playerData );
    }

    /// <summary>
    /// Вызывается когда игрок ОТКЛЮЧАЕТСЯ от сервера.
    /// Только на хосте.
    /// </summary>
    void Component.INetworkListener.OnDisconnected( Connection channel )
    {
        // Находим данные отключившегося игрока
        var pd = PlayerData.For( channel );
        if ( pd is not null )
        {
            // Удаляем его PlayerData (и самого игрока, если он ещё жив)
            pd.GameObject.Destroy();
        }
    }

    /// <summary>
    /// Создаёт PlayerData для подключившегося игрока.
    /// Если PlayerData уже существует (реконнект) — возвращает существующий.
    /// </summary>
    private PlayerData CreatePlayerInfo( Connection channel )
    {
        // Проверяем, нет ли уже данных для этого игрока
        var existingPlayerInfo = PlayerData.For( channel );
        if ( existingPlayerInfo.IsValid() )
            return existingPlayerInfo;

        // Создаём новый GameObject с компонентом PlayerData
        var go = new GameObject( true, $"PlayerInfo - {channel.DisplayName}" );
        var data = go.AddComponent<PlayerData>();
        data.SteamId = (long)channel.SteamId;
        data.PlayerId = channel.Id;
        data.DisplayName = channel.DisplayName;

        // Делаем объект сетевым (видимым всем игрокам)
        // null означает — владелец не назначен (им управляет хост)
        go.NetworkSpawn( null );
        go.Network.SetOwnerTransfer( OwnerTransfer.Fixed );

        return data;
    }

    /// <summary>
    /// Спавнит игрока на карте. Основной метод создания персонажа.
    /// </summary>
    public void SpawnPlayer( PlayerData playerData )
    {
        // Проверки безопасности
        Assert.NotNull( playerData, "PlayerData is null" );
        Assert.True( Networking.IsHost, 
            $"Client tried to SpawnPlayer: {playerData.DisplayName}" );

        // Проверяем — может этот игрок уже заспавнен?
        if ( Scene.GetAll<Player>().Any( 
            x => x.Network.Owner?.Id == playerData.PlayerId ) )
            return;

        // 1. Находим место для спавна
        var startLocation = FindSpawnLocation().WithScale( 1 );

        // 2. Клонируем префаб игрока
        var playerGo = GameObject.Clone( 
            "/prefabs/engine/player.prefab", 
            new CloneConfig 
            { 
                Name = playerData.DisplayName, 
                StartEnabled = false,     // Начинаем выключенным
                Transform = startLocation // Ставим на точку спавна
            });

        // 3. Связываем Player с его PlayerData
        var player = playerGo.Components.Get<Player>( true );
        player.PlayerData = playerData;

        // 4. Делаем объект сетевым и назначаем владельца
        var owner = Connection.Find( playerData.PlayerId );
        playerGo.NetworkSpawn( owner );

        // 5. Рассылаем событие "Игрок заспавнился"
        IPlayerEvent.PostToGameObject( player.GameObject, x => x.OnSpawned() );
    }

    /// <summary>
    /// Находит случайную точку спавна на карте.
    /// Ищет все компоненты SpawnPoint в сцене.
    /// </summary>
    Transform FindSpawnLocation()
    {
        // Ищем все точки спавна на карте
        var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();

        // Если точек нет — спавним в центре мира
        if ( spawnPoints.Length == 0 )
        {
            return Transform.Zero;
        }

        // Выбираем случайную точку
        return Random.Shared.FromArray( spawnPoints ).Transform.World;
    }
}
```

### Разбор процесса подключения игрока:

Вот что происходит пошагово, когда кто-то заходит на сервер:

```
1. Игрок нажимает "Join Game"
   ↓
2. Движок вызывает OnActive( Connection channel )
   ↓
3. CreatePlayerInfo() создаёт PlayerData
   - Создаёт новый GameObject "PlayerInfo - Имя"
   - Добавляет компонент PlayerData с SteamId, именем
   - NetworkSpawn() — делает видимым по сети
   ↓
4. SpawnPlayer() создаёт персонажа
   - FindSpawnLocation() — ищет SpawnPoint на карте
   - Clone() — копирует префаб player.prefab
   - Привязывает PlayerData
   - NetworkSpawn( owner ) — назначает владельца
   ↓
5. Игрок появляется на карте и может ходить!
```

### Почему `StartEnabled = false`?

Обрати внимание на строку:
```csharp
StartEnabled = false
```

Мы создаём игрока **выключенным**. Это нужно, чтобы успеть настроить все компоненты (привязать PlayerData, настроить сеть) **до того**, как они начнут работать. После `NetworkSpawn()` объект автоматически включается.

### Почему `channel.CanSpawnObjects = false`?

Это **защита от читов**. Если клиент мог бы создавать объекты напрямую, читер мог бы заспавнить что угодно. Вместо этого, все запросы на спавн идут через RPC на хост, и хост решает, разрешить или нет.

### `GameObject.Clone()` — как работает:

```csharp
GameObject.Clone( "/prefabs/engine/player.prefab", config );
```

Эта функция:
1. Читает файл `player.prefab` (JSON)
2. Создаёт **новый** GameObject со всеми компонентами из префаба
3. Применяет настройки из `config` (имя, позиция, включён/выключен)

Это как штамп — один шаблон, много копий.

### `NetworkSpawn()` — как работает:

```csharp
playerGo.NetworkSpawn( owner );
```

Эта функция:
1. Регистрирует объект в сетевой системе
2. Отправляет данные объекта **всем** подключённым клиентам
3. На каждом клиенте создаётся **прокси-копия** этого объекта
4. `owner` — соединение, которое **управляет** этим объектом

## Шаг 3.3: Про точки спавна (SpawnPoint)

`SpawnPoint` — это **встроенный** компонент движка s&box. Тебе не нужно его создавать! Карты (например `facepunch.flatgrass`) уже содержат эти точки.

Но если карта не содержит точек спавна, наш `MapPlayerSpawner` (из этапа 2) может служить альтернативой. В текущей версии мы используем стандартный `SpawnPoint`.

> 💡 В финальной версии мы проверяем и `SpawnPoint`, и `MapPlayerSpawner`. Сейчас для простоты используем только `SpawnPoint`.

## ✅ Проверка

Теперь ты можешь:

1. **Запустить игру** в редакторе s&box
2. **Увидеть своего персонажа** на карте
3. **Ходить** (WASD), **прыгать** (пробел), **смотреть** (мышь)
4. **Бегать** (Shift) и **приседать** (Ctrl)

Всё это работает благодаря встроенному `PlayerController` — мы не написали ни строчки кода для движения!

## Что мы создали:

- [x] `GameManager.cs` — глобальная система управления игрой
- [x] Подключение игрока → создание PlayerData → спавн Player
- [x] Отключение игрока → удаление его данных
- [x] Поиск точки спавна на карте
- [x] Понимание: GameObjectSystem, NetworkSpawn, Clone, INetworkListener

## Архитектура на данный момент:

```
Сцена
├── [GameObjectSystem] GameManager     ← управляет всем
├── MapLoader                          ← загружает карту
├── PlayerInfo - "Имя" (GameObject)    ← создаётся при подключении
│   └── PlayerData                     ← kills, deaths, имя
└── "Имя" (GameObject)                 ← создаётся SpawnPlayer()
    ├── PlayerController               ← движение (движок)
    ├── Player                         ← здоровье
    ├── SkinnedModelRenderer           ← 3D модель
    └── ...
```

---

**Далее**: [Этап 4: Здоровье и урон →](04_Здоровье_и_урон.md)
