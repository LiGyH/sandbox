# 00.21 — Сетевые объекты и NetworkHelper

## Что мы делаем?

Разбираем, **как заставить `GameObject` существовать у всех клиентов одновременно**. Это фундамент: игрок, пули, пропы, машины — всё что видно другим игрокам, должно быть «сетевым».

## Два способа сделать объект сетевым

### 1. Настроить в редакторе

1. Выдели `GameObject` в сцене.
2. В инспекторе найди секцию **Network**.
3. Поменяй **Network Mode** → **Network Object**.

Теперь при запуске сессии этот объект будет автоматически отсинхронизирован со всеми клиентами.

### 2. Создать в коде через `NetworkSpawn`

```csharp
// Клонируем префаб
var go = PlayerPrefab.Clone( spawnPoint.WorldTransform );

// Делаем его сетевым — рассылаем всем клиентам
go.NetworkSpawn();
```

После `NetworkSpawn()`:
- У всех остальных клиентов появится **прокси** этого объекта.
- По умолчанию **владельцем** становится тот, кто вызвал `NetworkSpawn()`.

## Уничтожение сетевого объекта

Просто:

```csharp
go.Destroy();
```

Это автоматически удалит его и у всех клиентов. Никакого `NetworkDestroy` нет.

## Компонент `NetworkHelper` — «стартер» сессии

Встроенный компонент, который решает три типовые задачи:

1. **Автоматически поднять сервер** при загрузке сцены, если не стоишь сейчас в гостях.
2. **Спавнить игрока**, когда кто-то подключается.
3. **Выбирать точку спавна** из списка.

### Как подключить

1. Создай `GameObject` в сцене, назови `NetworkHelper`.
2. **Add Component → Network Helper**.
3. Проставь свойства:
   - **Start Server** = `true` (автоматический запуск сервера).
   - **Player Prefab** = перетащи `prefabs/player.prefab`.
   - **Spawn Points** = список `GameObject`-ов точек спавна (или оставь пустым — будут спавниться на позиции NetworkHelper).

Всё. При запуске сцены NetworkHelper сам поднимет сервер, при каждом новом соединении — создаст копию твоего `PlayerPrefab`, назначит его владельцем нового игрока и поставит в случайный spawn point.

### Как NetworkHelper работает внутри

Упрощённо:

```csharp
public sealed class NetworkHelper : Component, Component.INetworkListener
{
    [Property] public bool StartServer { get; set; } = true;
    [Property] public GameObject PlayerPrefab { get; set; }
    [Property] public List<GameObject> SpawnPoints { get; set; }

    protected override async Task OnLoad()
    {
        if ( StartServer && !Networking.IsActive )
        {
            Networking.CreateLobby( new LobbyConfig() );
        }
    }

    void INetworkListener.OnActive( Connection conn )
    {
        var spawn = SpawnPoints.FirstOrDefault();
        var go = PlayerPrefab.Clone( spawn?.WorldTransform ?? Transform.World );
        go.NetworkSpawn( conn );   // назначить конкретному игроку владение
    }
}
```

`NetworkSpawn( conn )` сразу отдаёт владение подключившемуся — он будет управлять своим игроком. Все остальные увидят этого игрока как **proxy**.

## Проверка, что «это мой объект»

В любом компоненте сетевого объекта:

```csharp
protected override void OnUpdate()
{
    if ( IsProxy )
        return;  // этим управляет другой клиент

    // читаем инпут, двигаемся, стреляем
}
```

`IsProxy == true` означает:
- «Объект в сцене **есть**, но его **simulate-ит кто-то другой**».
- «Я не должен читать `Input`, не должен писать в `[Sync]` свойства».

## Orphan mode — что делать, если владелец вышел

По умолчанию, когда хозяин объекта отключается, объект **удаляется у всех**. Для пропов и машин это неудобно — лучше оставить их в мире и передать хосту. Настраивается:

```csharp
go.Network.SetOrphanedMode( NetworkOrphaned.Host );       // хост становится владельцем
go.Network.SetOrphanedMode( NetworkOrphaned.ClearOwner ); // без владельца, симулирует хост
go.Network.SetOrphanedMode( NetworkOrphaned.Random );     // случайный клиент
go.Network.SetOrphanedMode( NetworkOrphaned.Destroy );    // удалить (по умолчанию)
```

## Какие данные автоматически передаются по сети

По умолчанию для сетевого объекта с клиента владельца каждому другому отправляются:

- **`WorldTransform`** — позиция, поворот, масштаб.
- **Все свойства с `[Sync]`** — см. [00.24](00_24_Sync_Properties.md).
- **Список компонентов и их наличие/отсутствие** — если владелец добавил компонент, прокси тоже его получат.

Изменения, которые делает **прокси** (не владелец), **не** уходят по сети. Кроме как через RPC к владельцу или хосту.

## Частые ошибки

- ❌ **Забыть `NetworkSpawn()`** после `Clone()`. Клонируется локально, для других игроков объекта как бы нет.
- ❌ **Двигать прокси-объект**. Ты двигаешь у себя, но другие не видят. Надо передать владение или слать RPC.
- ❌ **Запустить ту же логику и у прокси, и у владельца**. Получишь двойной выстрел, двойные звуки. Проверка `if ( IsProxy ) return;` в ключевых местах обязательна.

## Результат

После этого этапа ты знаешь:

- ✅ Как сделать `GameObject` сетевым (через редактор или `NetworkSpawn()`).
- ✅ Что делает `NetworkHelper` и как его настроить.
- ✅ Что такое proxy-объект и зачем проверять `IsProxy`.
- ✅ Что происходит при отключении владельца и как это настроить.
- ✅ Какие данные автоматически синхронизируются (transform + `[Sync]`).

---

📚 **Facepunch docs:**
- [networking/networked-objects.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/networked-objects.md)
- [networking/network-helper.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/network-helper.md)

**Следующий шаг:** [00.22 — Ownership (владение объектом)](00_22_Ownership.md)
