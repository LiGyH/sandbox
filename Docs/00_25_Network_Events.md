# 00.25 — События сети (INetworkListener и др.)

## Что мы делаем?

Разбираем **сетевые события** — какие интерфейсы можно реализовать в компонентах, чтобы узнать «кто-то подключился», «кто-то отключился», «объект стал сетевым» и т.д. Это финальный кусочек фундамента по сети.

## `Component.INetworkListener` — события сессии

Встраиваемый интерфейс. Методы **вызываются только на хосте**, потому что только он знает о всех соединениях.

```csharp
public sealed class GameDirector : Component, Component.INetworkListener
{
    void INetworkListener.OnActive( Connection conn )
    {
        Log.Info( $"Подключился: {conn.DisplayName} (Steam {conn.SteamId})" );
        // ...спавнить игрока, слать приветствие, писать в чат
    }

    void INetworkListener.OnDisconnected( Connection conn )
    {
        Log.Info( $"Отключился: {conn.DisplayName}" );
        // ...сохранить статистику, передать его пропы хосту
    }

    void INetworkListener.OnBecameHost( Connection oldHost )
    {
        Log.Info( "Я стал новым хостом" );
        // ...перехватить управление сервером
    }
}
```

Вызовы:

| Метод | Когда | Где |
|---|---|---|
| `OnActive( conn )` | Новое соединение активно | На хосте |
| `OnDisconnected( conn )` | Соединение разорвалось | На хосте |
| `OnBecameHost( old )` | Хост передал эстафету тебе | На новом хосте |
| `OnConnected( conn )` | Клиент присоединился (раньше Active) | На хосте |

## `IGameObjectNetworkEvents` — события объекта

Подписываешься **в компоненте, прикреплённом к сетевому `GameObject`**. Получаешь события, касающиеся этого объекта.

```csharp
public sealed class Prop : Component, Component.INetworkListener, IGameObjectNetworkEvents
{
    void IGameObjectNetworkEvents.OnNetworkSpawn( Connection owner )
    {
        Log.Info( $"Я появился в сети. Владелец: {owner?.DisplayName ?? "хост"}" );
    }

    void IGameObjectNetworkEvents.OnOwnerChanged( Connection oldOwner, Connection newOwner )
    {
        Log.Info( $"Хозяин сменился: {oldOwner?.DisplayName} → {newOwner?.DisplayName}" );
    }
}
```

Типичные методы:

| Метод | Смысл |
|---|---|
| `OnNetworkSpawn` | Объект только что стал сетевым |
| `OnNetworkDespawn` | Перестал быть сетевым |
| `OnOwnerChanged` | Сменился владелец |
| `OnHostChanged` | Сменился хост сессии |

## `IScenePhysicsEvents` — физические события

Не совсем про сеть, но в этом же семействе — способ узнать о событиях физики:

```csharp
public sealed class PhysicsWatcher : Component, IScenePhysicsEvents
{
    void IScenePhysicsEvents.PrePhysicsStep() { /* перед физикой */ }
    void IScenePhysicsEvents.PostPhysicsStep() { /* после */ }
}
```

## `ISceneStartup` — события запуска сцены

```csharp
public sealed class Initializer : Component, ISceneStartup
{
    void ISceneStartup.OnHostPreStart()
    {
        // хост готовится запустить сервер, но ещё не запустил
    }

    void ISceneStartup.OnHostInitialize()
    {
        // хост инициализирует состояние
    }
}
```

Используется, если нужно **подготовить что-то** до того, как кто-то начнёт подключаться.

## Практический паттерн: спавн игроков

```csharp
public sealed class GameManager : Component, Component.INetworkListener
{
    [Property] public GameObject PlayerPrefab { get; set; }
    [Property] public List<GameObject> SpawnPoints { get; set; }

    void INetworkListener.OnActive( Connection conn )
    {
        var spawn = Game.Random.FromList( SpawnPoints );
        var player = PlayerPrefab.Clone( spawn.WorldTransform );
        player.NetworkSpawn( conn );   // владельцем становится подключившийся
    }

    void INetworkListener.OnDisconnected( Connection conn )
    {
        // найти его игрока и передать хосту (или удалить)
        var playerObj = Scene.GetAllComponents<Player>()
            .FirstOrDefault( p => p.Network.OwnerId == conn.Id );
        playerObj?.GameObject.Destroy();
    }
}
```

Это почти ровно то, что делает `NetworkHelper` (см. [00.21](00_21_Networked_Objects.md)).

## Практический паттерн: логирование

```csharp
public sealed class ServerLogger : Component, Component.INetworkListener
{
    void INetworkListener.OnActive( Connection c )
        => Chat.AddText( $"✅ {c.DisplayName} подключился" );

    void INetworkListener.OnDisconnected( Connection c )
        => Chat.AddText( $"❌ {c.DisplayName} отключился" );
}
```

## Важные нюансы

- **Не забывай, где вызывается.** Большинство `INetworkListener` методов **только на хосте**. Не пытайся через них показать UI — у других клиентов они не вызовутся.
- **Компонент должен быть активен.** Если `GameObject.Enabled = false` или компонент выключен, интерфейс не вызывается.
- **Не делай тяжёлой работы в `OnActive`.** Новый клиент ждёт — чем быстрее отработают коллбеки, тем быстрее он попадёт в игру.

## Результат

После этого этапа ты знаешь:

- ✅ Как подписаться на сетевые события через `Component.INetworkListener`.
- ✅ Какие события есть (`OnActive`, `OnDisconnected`, `OnBecameHost`).
- ✅ Что `IGameObjectNetworkEvents` даёт события конкретного объекта (`OnNetworkSpawn`, `OnOwnerChanged`).
- ✅ Типичный паттерн спавна игроков в `OnActive`.
- ✅ Что `INetworkListener` выполняется только на хосте.

---

📚 **Facepunch docs:**
- [scene/components/events/igameobjectnetworkevents.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/components/events/igameobjectnetworkevents.md)
- [scene/components/events/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/scene/components/events/index.md)

**Следующий шаг:** [00.26 — Razor Panels: основы UI](00_26_Razor_Basics.md)
