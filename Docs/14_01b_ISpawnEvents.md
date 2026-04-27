# 14.01b — Интерфейс событий спавна (ISpawnEvents) 📡

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.25 — События сети (ISceneEvent)](00_25_Network_Events.md)
> - [00.12 — Component](00_12_Component.md)

> **🔗 Связанные документы:**
>
> - [14.01 — ISpawner](14_01_ISpawner.md) — что именно создаётся
> - [14.06 — SpawnerWeapon](14_06_SpawnerWeapon.md) — кто стреляет события для меню спавна
> - [04.01 — GameManager](04_01_GameManager.md) — кто стреляет события для консольного `spawn`
> - [04.06 — LimitsSystem](04_06_LimitsSystem.md) — пример слушателя

## Что мы делаем?

Создаём **`ISpawnEvents`** — интерфейс сценовых событий, который позволяет любому компоненту/`GameObjectSystem` подписаться на спавн объектов через `ISpawner` (пропы, сущности, маунты, дупы). Можно отменить спавн ещё до создания (`OnSpawn`) или просто получить список созданных объектов после факта (`OnPostSpawn`).

## Зачем это нужно?

Раньше системы лимитов, античитов, наград и т.п. были вынуждены либо хачить `SpawnerWeapon`/`GameManager`, либо сканировать сцену в `OnUpdate`. С `ISpawnEvents` появляется чистая точка расширения:

- Лимиты на пропы/взрывчатку (см. `LimitsSystem` — [04.06](04_06_LimitsSystem.md))
- Античит (отклонить спавн запрещённых моделей)
- Логирование/статистика спавна
- Автодобавление компонентов (например, кастомный `Ownable`-аналог)
- Защита по правам/зонам карты

Всё это пишется как обычный `Component` или `GameObjectSystem`, реализующий `ISpawnEvents` — никакой регистрации, движок сам диспатчит вызовы через `Scene.RunEvent<ISpawnEvents>(...)`.

## Как это работает внутри движка?

### Базовое наследование

```csharp
public interface ISpawnEvents : ISceneEvent<ISpawnEvents>
{
    void OnSpawn( SpawnData e ) { }
    void OnPostSpawn( PostSpawnData e ) { }
}
```

`ISceneEvent<T>` — стандартный интерфейс s&box: любой компонент сцены, реализующий `ISpawnEvents`, автоматически попадает в список получателей `Scene.RunEvent<ISpawnEvents>(...)`. Методы — с дефолтной реализацией, поэтому реализовывать обязательно только то, что нужно.

### Данные `SpawnData` и `PostSpawnData`

```csharp
public class SpawnData
{
    public ISpawner Spawner { get; init; }   // что спавним (PropSpawner, EntitySpawner, MountSpawner, DuplicatorSpawner)
    public Transform Transform { get; init; } // куда (мир)
    public PlayerData Player { get; init; }   // кто инициировал
    public bool Cancelled { get; set; }       // <-- установить true, чтобы отменить
}

public class PostSpawnData : SpawnData
{
    public List<GameObject> Objects { get; init; } // что в итоге появилось
}
```

- `init`-сеттеры — поля заполняются один раз создателем события.
- `Cancelled` — единственное мутируемое поле в `SpawnData`. Если хотя бы один слушатель установит его в `true`, инициатор спавна **не зовёт** `Spawner.Spawn(...)` и **не стреляет** `OnPostSpawn`.

### Кто стреляет события

| Источник спавна | Файл | Когда |
|------------------|------|-------|
| Консольная команда `spawn` (RPC `GameManager.Spawn`) | `Code/GameLoop/GameManager.Spawn.cs` → `SpawnAndUndo` | `OnSpawn` до создания, `OnPostSpawn` после успешного спавна с непустым списком объектов |
| Меню спавна (оружие `SpawnerWeapon`) | `Code/Spawner/SpawnerWeapon.cs` → `DoSpawn` | То же |

В обоих случаях идиома одинакова:

```csharp
var spawnData = new ISpawnEvents.SpawnData
{
    Spawner = spawner,
    Transform = transform,
    Player = player?.PlayerData
};

Scene.RunEvent<ISpawnEvents>( x => x.OnSpawn( spawnData ) );

if ( spawnData.Cancelled )
    return;

var objects = await spawner.Spawn( transform, player );

if ( objects is { Count: > 0 } )
{
    // … добавить в Undo …

    Scene.RunEvent<ISpawnEvents>( x => x.OnPostSpawn( new ISpawnEvents.PostSpawnData
    {
        Spawner = spawner,
        Transform = transform,
        Player = player?.PlayerData,
        Objects = objects
    } ) );
}
```

> ⚠️ События стреляются **только на хосте** — там же, где живёт реальная физика и сетевой авторитет. Клиенты их не видят. Если нужна реакция на клиенте, делайте отдельный RPC.

## Создай файл

Путь: `Code/Spawner/ISpawnEvents.cs`

```csharp
/// <summary>
/// Allows listening to spawn events across the scene.
/// Implement this on a <see cref="Component"/> to receive callbacks before
/// and after objects are spawned.
/// </summary>
public interface ISpawnEvents : ISceneEvent<ISpawnEvents>
{
    public class SpawnData
    {
        public ISpawner Spawner { get; init; }
        public Transform Transform { get; init; }
        public PlayerData Player { get; init; }
        public bool Cancelled { get; set; }
    }

    public class PostSpawnData : SpawnData
    {
        public List<GameObject> Objects { get; init; }
    }

    void OnSpawn( SpawnData e ) { }
    void OnPostSpawn( PostSpawnData e ) { }
}
```

## Пример: запретить спавн взрывчатки в зоне

```csharp
public sealed class NoExplosivesZone : Component, ISpawnEvents
{
    [Property] public BBox Zone { get; set; }

    void ISpawnEvents.OnSpawn( ISpawnEvents.SpawnData e )
    {
        if ( e.Spawner is PropSpawner p
             && p.Model?.Data?.Explosive == true
             && Zone.Contains( e.Transform.Position ) )
        {
            e.Cancelled = true;
        }
    }
}
```

Положи этот компонент на любой `GameObject` на сцене — событие приедет автоматически.

## Пример: лог в чат

```csharp
public sealed class SpawnLog : GameObjectSystem<SpawnLog>, ISpawnEvents
{
    public SpawnLog( Scene scene ) : base( scene ) { }

    void ISpawnEvents.OnPostSpawn( ISpawnEvents.PostSpawnData e )
    {
        Scene.Get<Chat>()?.AddSystemText(
            $"{e.Player?.DisplayName} spawned {e.Spawner.DisplayName} ({e.Objects.Count} obj.)" );
    }
}
```

## Проверка

- Любая команда `spawn prop:...` должна сначала пройти через `OnSpawn` всех подписчиков и только потом создавать объект.
- Если `LimitsSystem` (см. [04.06](04_06_LimitsSystem.md)) включена — превышение лимита отменяет спавн.
- Спавн через UI меню (`SpawnerWeapon`) ведёт себя точно так же, как консольный `spawn` — обе точки используют одинаковые события.
- Уничтоженный объект, попавший в `e.Objects`, нормально обрабатывается слушателями (`go.IsValid()` = false).

---

## ➡️ Следующий шаг

Переходи к **[14.02 — PropSpawner](14_02_PropSpawner.md)** или к параллельному интерфейсу для тулгана — **[09.11 — IToolActionEvents](09_11_IToolActionEvents.md)**.
