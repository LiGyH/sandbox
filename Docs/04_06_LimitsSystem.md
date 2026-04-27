# 04.06 — Система лимитов (LimitsSystem) 🚦

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.15 — Scene и GameObjectSystem](00_15_Scene_GameObjectSystem.md)
> - [00.18 — ConVar и ConCmd](00_18_ConVar_ConCmd.md)
> - [00.25 — События сети](00_25_Network_Events.md)

> **🔗 Связанные новые интерфейсы (этот же апдейт исходников):**
>
> - [14.01b — ISpawnEvents](14_01b_ISpawnEvents.md) — события спавна (`OnSpawn`/`OnPostSpawn`)
> - [09.11 — IToolActionEvents](09_11_IToolActionEvents.md) — события действий тулгана

## Что мы делаем?

Создаём **`LimitsSystem`** — серверную `GameObjectSystem`, которая ограничивает, сколько объектов один игрок может заспавнить или поставить тулганом. Лимиты задаются `ConVar`-ами (например `sb.limit.props 50`) и отдельно проверяются для пропов, взрывчатки, констрейнтов, шариков, эмиттеров, ускорителей, ховерболлов и колёс.

## Зачем это нужно?

На общем сервере без лимитов пара игроков могут заспавнить тысячи пропов и положить производительность. `LimitsSystem` решает это:

- Серверная настройка через `ConVar` с флагом `GameSetting` — админ сервера крутит ползунки.
- Проверка делается **в момент спавна** (через `ISpawnEvents.OnSpawn`) и **в момент действия тулгана** (через `IToolActionEvents.OnToolAction`) — лишний объект просто не создаётся, никаких пост-удалений.
- Учёт идёт **per-player по `SteamId`** — то, что заспавнил один игрок, не мешает другому.
- Дупликаты проверяются батчем: если внутри дупа 200 пропов, а у игрока осталось 50 — весь дупа отклоняется одной проверкой, до спавна.
- Игрок получает уведомление через `Notices` (см. [05.08](05_08_Notices.md)).

## Как это работает внутри движка?

### Архитектура

```
LimitsSystem : GameObjectSystem<LimitsSystem>,
                ISpawnEvents,         // OnSpawn / OnPostSpawn
                IToolActionEvents     // OnToolAction / OnPostToolAction
  ├── ConVar-свойства MaxPropsPerPlayer, MaxExplosivesPerPlayer, …
  ├── _tracked: Dictionary<long, List<GameObject>>  — список объектов на игрока (по SteamId)
  └── _allTracked: HashSet<GameObject>              — быстрый dedup
```

Подписка на события — автоматическая: интерфейсы `ISpawnEvents` и `IToolActionEvents` наследуют `ISceneEvent<...>`, поэтому движок диспатчит вызов на любой компонент/систему, реализующую их.

### ConVar-лимиты

Все лимиты — `static int` со значением по умолчанию `-1` (без ограничений). Значение `0` означает «вообще нельзя». Атрибуты:

- `[ConVar( "sb.limit.<...>", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting )]` — реплицируется клиентам, меняется только сервером, отображается в UI настроек игры.
- `[Range( -1, N )]` — ползунок в редакторе/настройках.
- `[Title( ... ), Group( "Limits" )]` — отображаемое имя и группа.

| ConVar | Свойство | Дефолт | Диапазон | Что считает |
|--------|----------|--------|----------|-------------|
| `sb.limit.props` | `MaxPropsPerPlayer` | -1 | -1…1024 | Все пропы (`Prop`) |
| `sb.limit.explosives` | `MaxExplosivesPerPlayer` | -1 | -1…16 | Пропы с `Model.Data.Explosive == true` |
| `sb.limit.balloons` | `MaxBalloons` | -1 | -1…64 | Объекты с `BalloonEntity` |
| `sb.limit.constraints` | `MaxConstraints` | -1 | -1…512 | Объекты с тегом `"constraint"` |
| `sb.limit.emitters` | `MaxEmitters` | -1 | -1…64 | `EmitterEntity` |
| `sb.limit.thrusters` | `MaxThrusters` | -1 | -1…64 | `ThrusterEntity` |
| `sb.limit.hoverballs` | `MaxHoverballs` | -1 | -1…32 | `HoverballEntity` |
| `sb.limit.wheels` | `MaxWheels` | -1 | -1…32 | `WheelEntity` |

### Семантика лимита

```csharp
private static bool IsExceeded( int limit, int count )
    => limit >= 0 && count >= limit;
```

- `limit == -1` — `IsExceeded` всегда `false` (без ограничений).
- `limit == 0` — `IsExceeded` всегда `true` (нельзя ничего создать).
- `limit > 0` — нельзя превысить (`>=` означает «уже на пределе»).

### Хранение объектов игрока

```csharp
private readonly Dictionary<long, List<GameObject>> _tracked = new();
private readonly HashSet<GameObject> _allTracked = new();
```

- Ключ словаря — `SteamId` (`long`), а не `Connection.Id` — выживает реконнект.
- `_allTracked` — быстрый дедуп, чтобы один и тот же объект не зачёлся дважды.
- Метод `Count( steamId, filter )` итерирует список **только этого игрока** (а не всю сцену), попутно пропалывая уничтоженные `GameObject`-ы.

### Поток `OnSpawn` (через ISpawnEvents)

Срабатывает на хосте до создания объекта (`SpawnerWeapon.DoSpawn` / `GameManager.SpawnAndUndo`):

1. Если `e.Spawner is DuplicatorSpawner` — батч-проверка: считаем сколько пропов и взрывчатки в дупе, отклоняем целиком если перерастёт лимит.
2. Если `e.Spawner is PropSpawner` — обычная проверка `MaxPropsPerPlayer`.
3. Дополнительно — `MaxExplosivesPerPlayer`, если спавнится взрывной проп (см. `IsExplosiveSpawn`).
4. При превышении — `e.Cancelled = true` и уведомление игроку.

### Поток `OnPostSpawn`

После успешного спавна — **трекаем** созданные объекты:

```csharp
void ISpawnEvents.OnPostSpawn( ISpawnEvents.PostSpawnData e )
    => Track( e.Player.SteamId, e.Objects );
```

### Поток `OnToolAction` (через IToolActionEvents)

Срабатывает когда игрок нажимает кнопку тулгана:

- `Reload` пропускается (это обычно «копировать настройки», а не создание).
- Универсальный `CheckToolLimit<TTool, TEntity>` смотрит, не превысил ли игрок лимит для своего набора (`TEntity` — компонент, по которому считается; `TTool` — тип `ToolMode`).
- Для констрейнтов (`BaseConstraintToolMode` или `KeepUpright`) — отдельная ветка с подсчётом по тегу `"constraint"`.
- Параметр `creationInput` нужен потому, что не все нажатия создают объект: например у `EmitterTool` создание идёт только на `ToolInput.Primary`, а `Secondary`/`Reload` — управление существующим эмиттером.

### Поток `OnPostToolAction`

После успешного действия тулгана — **трекаем** объекты, которые тул положил в `CreatedObjects`. За это в `ToolMode` отвечает метод `Track(...)` (см. [09.01](09_01_ToolMode.md)).

### Уведомление игрока

```csharp
Notices.SendNotice( target, "block", Color.Red,
    $"Limit reached: {category} ({limit})", 3 );
```

Игроку показывается красная плашка `"Limit reached: props (50)"` на 3 секунды. Иконка `"block"` — стандартная иконка Material Icons.

## Создай файл

Путь: `Code/GameLoop/LimitsSystem.cs`

Полный исходник — в репозитории. Ниже — каркас, чтобы было понятно как всё собирается:

```csharp
public sealed class LimitsSystem
    : GameObjectSystem<LimitsSystem>, ISpawnEvents, IToolActionEvents
{
    [Range( -1, 1024 )]
    [Title( "Max Props Per Player" ), Group( "Limits" )]
    [ConVar( "sb.limit.props",
        ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting,
        Help = "Maximum props per player. -1 = unlimited, 0 = none allowed." )]
    public static int MaxPropsPerPlayer { get; set; } = -1;

    // … другие ConVar-лимиты по тому же шаблону …

    public LimitsSystem( Scene scene ) : base( scene ) { }

    private static bool IsExceeded( int limit, int count )
        => limit >= 0 && count >= limit;

    void ISpawnEvents.OnSpawn( ISpawnEvents.SpawnData e )      { /* … */ }
    void ISpawnEvents.OnPostSpawn( ISpawnEvents.PostSpawnData e ) { /* … */ }

    void IToolActionEvents.OnToolAction( IToolActionEvents.ActionData e ) { /* … */ }
    void IToolActionEvents.OnPostToolAction( IToolActionEvents.PostActionData e ) { /* … */ }

    private bool CheckToolLimit<TTool, TEntity>(
        IToolActionEvents.ActionData e, int limit,
        ToolInput? creationInput = null )
        where TTool : ToolMode where TEntity : Component
    { /* … */ }
}
```

## Как это интегрировано

| Кто стреляет события | Где |
|----------------------|-----|
| `ISpawnEvents.OnSpawn` / `OnPostSpawn` | `GameManager.Spawn.cs` (консольный `spawn`) и `SpawnerWeapon.DoSpawn` (см. [04.01](04_01_GameManager.md), [14.06](14_06_SpawnerWeapon.md)) |
| `IToolActionEvents.OnToolAction` / `OnPostToolAction` | `ToolMode.DispatchActions` — оборачивает каждое зарегистрированное `RegisterAction` (см. [09.01](09_01_ToolMode.md)) |

`LimitsSystem` ни на кого не «подписывается» вручную — она просто реализует оба интерфейса, и движок сам вызывает её через `Scene.RunEvent<...>`.

## Проверка

1. На хосте в консоли: `sb.limit.props 5` → попробуй заспавнить 6 пропов одним игроком — на 6-м появится красная плашка «Limit reached: props (5)», объект не появится.
2. `sb.limit.constraints 0` → любая попытка применить Weld/Rope/Slider/Ball/Elastic/Hydraulic/NoCollide/KeepUpright отклоняется.
3. `sb.limit.balloons 3` + `attack2` (без верёвки) на инструменте Balloon — на 4-м шарике отказ.
4. Загрузи дупа с 200 пропов при `sb.limit.props 50` — отклоняется целиком, никаких частично заспавненных кусков не остаётся.
5. После реконнекта игрока его счётчик не сбрасывается (ключ — `SteamId`).

## Ссылки на движок

- [`GameObjectSystem<T>`](https://github.com/Facepunch/sbox-docs/) — синглтон сцены.
- [`ConVarFlags.GameSetting`](https://github.com/Facepunch/sbox-docs/) — лимиты автоматически попадают в UI настроек игры.
- [`ISceneEvent<T>`](https://github.com/Facepunch/sbox-docs/) — базовый интерфейс для `ISpawnEvents` и `IToolActionEvents`.

---

## ➡️ Следующий шаг

Возвращайся к разделу **Фаза 4** или переходи к **[14.01b — ISpawnEvents](14_01b_ISpawnEvents.md)** / **[09.11 — IToolActionEvents](09_11_IToolActionEvents.md)**, чтобы посмотреть на сами события, которые слушает `LimitsSystem`.
