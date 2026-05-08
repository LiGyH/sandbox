# 02.03 — События игрока (PlayerEvent) 📡

## Что мы делаем?

Этот файл создаёт **систему событий игрока** — два интерфейса, через которые компоненты узнают о действиях игрока: спавн, получение урона, смерть, прыжок, приземление, подбор/выброс/смена/удаление оружия, перемещение слотов и настройка камеры.

Представьте, что игрок прыгнул. Об этом должны узнать: аниматор (запустить анимацию прыжка), звуковая система (проиграть звук), UI (показать эффект), камера (подпрыгнуть). Вместо того чтобы вызывать каждую систему вручную, мы **рассылаем событие**, и все подписчики реагируют самостоятельно.

## Как это работает внутри движка?

### Паттерн ISceneEvent

В s&box `ISceneEvent<T>` — это механизм рассылки событий. Когда вы вызываете:

```csharp
Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnJump() );
Global.IPlayerEvents.Post( e => e.OnPlayerJumped( player ) );
```

Движок находит все компоненты, реализующие нужный интерфейс, и вызывает у них метод. Это паттерн **«наблюдатель»** (Observer) — компоненты подписываются, просто реализуя интерфейс.

### Два уровня событий — `Local` и `Global`

Раньше существовали два независимых интерфейса (`IPlayerEvent` и `ILocalPlayerEvent`). В актуальной версии sandbox они переименованы и сгруппированы в **partial-классы-неймспейсы** `Local` и `Global` — чтобы было сразу видно, кто их получает:

```
Local.IPlayerEvents (на GameObject игрока)
  └── Получают только компоненты на самом игроке
      Пример: PlayerInventory, PlayerLoadout, PlayerStats, PlayerFallDamage

Global.IPlayerEvents (на всю сцену)
  └── Получают все компоненты в сцене
      Пример: HUD, лента убийств (Feed), музыка, камера, эффекты окружения
```

**Почему именно два интерфейса?** Не всё должно знать обо всём. Контроллер здоровья находится на объекте игрока — ему нужен `Local.IPlayerEvents`. А вот HUD — это отдельный объект в сцене — ему нужен `Global.IPlayerEvents`.

> 💡 То же самое сделано для других системных событий — см. `Global.ISaveEvents` ([23.01](23_01_ISaveEvents.md)) и `Global.ISpawnEvents` ([14.01b](14_01b_ISpawnEvents.md)).

### Структуры/классы параметров

Вместо длинного списка аргументов (`float damage, GameObject attacker, GameObject weapon, ...`) используются **структуры** (`struct`) и **классы-события** (`class`):

- `struct` — для иммутабельных пакетов данных (`PlayerDiedParams`, `PlayerDamageParams`).
- `class` с полями `Cancelled` — для событий, которые слушатели могут **отменить** (`PlayerPickupEvent`, `PlayerDropEvent`, `PlayerDamageEvent`, …).

Преимущества: легко добавлять новые поля, не ломая существующий код, и можно отменить действие, выставив `e.Cancelled = true`.

## Создай файл

Путь: `Code/Player/PlayerEvent.cs`

```csharp
public struct PlayerDiedParams
{
    public GameObject Attacker { get; set; }
}

public struct PlayerDamageParams
{
    public float Damage { get; set; }
    public GameObject Attacker { get; set; }
    public GameObject Weapon { get; set; }
    public TagSet Tags { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Origin { get; set; }
}

public class PlayerPickupEvent
{
    public Player Player { get; init; }
    public BaseCarryable Weapon { get; init; }
    public int Slot { get; init; }
    public bool Cancelled { get; set; }
}

public class PlayerDropEvent
{
    public Player Player { get; init; }
    public BaseCarryable Weapon { get; init; }
    public bool Cancelled { get; set; }
}

public class PlayerSwitchWeaponEvent
{
    public Player Player { get; init; }
    public BaseCarryable From { get; init; }
    public BaseCarryable To { get; init; }
    public bool Cancelled { get; set; }
}

public class PlayerRemoveWeaponEvent
{
    public Player Player { get; init; }
    public BaseCarryable Weapon { get; init; }
    public bool Cancelled { get; set; }
}

public class PlayerMoveSlotEvent
{
    public Player Player { get; init; }
    public int FromSlot { get; init; }
    public int ToSlot { get; init; }
    public bool Cancelled { get; set; }
}

public class PlayerDamageEvent
{
    public Player Player { get; init; }
    public DamageInfo DamageInfo { get; init; }
    public float Damage { get; set; }
    public bool Cancelled { get; set; }
}

public class PlayerRespawnEvent
{
    public PlayerData PlayerData { get; init; }
    public Transform SpawnLocation { get; set; }
}

public class PlayerKillEvent
{
    public Player Player { get; init; }
    public GameObject Victim { get; init; }
    public DamageInfo DamageInfo { get; init; }
}

/// <summary>
/// Events fired only to the Player's own GameObject hierarchy.
/// </summary>
public static partial class Local
{
    public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
    {
        void OnSpawned() { }
        void OnDied( PlayerDiedParams args ) { }
        void OnDamage( PlayerDamageParams args ) { }
        void OnJump() { }
        void OnLand( float distance, Vector3 velocity ) { }
        void OnSuicide() { }
        void OnPickup( PlayerPickupEvent e ) { }
        void OnDrop( PlayerDropEvent e ) { }
        void OnSwitchWeapon( PlayerSwitchWeaponEvent e ) { }
        void OnRemoveWeapon( PlayerRemoveWeaponEvent e ) { }
        void OnMoveSlot( PlayerMoveSlotEvent e ) { }
        void OnDamaging( PlayerDamageEvent e ) { }
        void OnKill( PlayerKillEvent e ) { }
        void OnCameraMove( ref Angles angles ) { }
        void OnCameraSetup( CameraComponent camera ) { }
        void OnCameraPostSetup( CameraComponent camera ) { }
    }
}

/// <summary>
/// Events broadcasted to the entire scene for any player action.
/// </summary>
public static partial class Global
{
    public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
    {
        void OnPlayerSpawned( Player player ) { }
        void OnPlayerDied( Player player, PlayerDiedParams args ) { }
        void OnPlayerDamage( Player player, PlayerDamageParams args ) { }
        void OnPlayerJumped( Player player ) { }
        void OnPlayerLanded( Player player, float distance, Vector3 velocity ) { }
        void OnPlayerSuicide( Player player ) { }
        void OnPlayerPickup( PlayerPickupEvent e ) { }
        void OnPlayerDrop( PlayerDropEvent e ) { }
        void OnPlayerSwitchWeapon( PlayerSwitchWeaponEvent e ) { }
        void OnPlayerRemoveWeapon( PlayerRemoveWeaponEvent e ) { }
        void OnPlayerMoveSlot( PlayerMoveSlotEvent e ) { }
        void OnPlayerDamaging( PlayerDamageEvent e ) { }
        void OnPlayerRespawning( PlayerRespawnEvent e ) { }
        void OnPlayerKill( PlayerKillEvent e ) { }
    }
}
```

## Разбор кода

### Интерфейс `Local.IPlayerEvents`

```csharp
public static partial class Local
{
    public interface IPlayerEvents : ISceneEvent<IPlayerEvents> { ... }
}
```

- Объявлен внутри `partial class Local` — это даёт «неймспейс-подобный» префикс при использовании: `Local.IPlayerEvents`.
- Наследует `ISceneEvent<IPlayerEvents>` — подключается к системе рассылки событий движка.
- События получают **только компоненты на GameObject игрока**, поэтому стрелять их надо адресно:

```csharp
Local.IPlayerEvents.PostToGameObject( Player.GameObject, e => e.OnDied( args ) );
```

### Интерфейс `Global.IPlayerEvents`

```csharp
public static partial class Global
{
    public interface IPlayerEvents : ISceneEvent<IPlayerEvents> { ... }
}
```

Получают **все** компоненты в сцене. Сюда стреляют, когда событие интересно «всему миру» — HUD, ленте убийств, музыкальной системе, камере, эффектам окружения, статистике сервера.

Обратите внимание, что у глобальных методов первым параметром идёт `Player player` (или сам ивент содержит `Player`) — слушатель в сцене не знает заранее, о ком идёт речь, поэтому имя игрока приходит в аргументах.

```csharp
Global.IPlayerEvents.Post( e => e.OnPlayerDied( player, args ) );
```

### Параметры урона и смерти (`struct`)

```csharp
public struct PlayerDamageParams
{
    public float Damage { get; set; }
    public GameObject Attacker { get; set; }
    public GameObject Weapon { get; set; }
    public TagSet Tags { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Origin { get; set; }
}
```

| Поле | Назначение |
|---|---|
| `Damage` | Количество урона (например, `25.0f`). |
| `Attacker` | Кто нанёс урон (GameObject атакующего). |
| `Weapon` | Чем нанёс урон (GameObject оружия). |
| `Tags` | Теги урона: `"head"` (хедшот), `"explosion"` (взрыв) — см. [DamageTags (02.04)](02_04_DamageTags.md). |
| `Position` | Точка попадания в мире (где именно ударила пуля). |
| `Origin` | Откуда пришёл урон (позиция стрелка). |

`struct` вместо `class` — потому что параметры урона одноразовые: создали, передали, забыли. Struct хранится на стеке и не нагружает сборщик мусора.

`PlayerDiedParams` устроен аналогично, но содержит только `Attacker`.

### Отменяемые события (`class` + `Cancelled`)

```csharp
public class PlayerPickupEvent
{
    public Player Player { get; init; }
    public BaseCarryable Weapon { get; init; }
    public int Slot { get; init; }
    public bool Cancelled { get; set; }
}
```

Слушатель может выставить `e.Cancelled = true`, и инициатор события (`PlayerInventory.Pickup` и т.п.) откатит действие. Так реализованы лимиты, античит и доступы по правам.

| Класс события | Где стреляется | Что можно отменить |
|---|---|---|
| `PlayerPickupEvent` | `PlayerInventory.Pickup` / `Take` | Подбор оружия |
| `PlayerDropEvent` | `PlayerInventory.Drop` | Выброс оружия |
| `PlayerSwitchWeaponEvent` | `PlayerInventory.SwitchWeapon` | Переключение слота |
| `PlayerRemoveWeaponEvent` | `PlayerInventory.Remove` | Удаление оружия |
| `PlayerMoveSlotEvent` | `PlayerInventory.MoveSlot` | Перенос/обмен слотов |
| `PlayerDamageEvent` | `Player.TakeDamage` (pre-damage) | Применение урона; можно править `Damage` |
| `PlayerRespawnEvent` | до спавна игрока | Менять `SpawnLocation` |
| `PlayerKillEvent` | при убийстве цели | (только уведомление) |

### События действий

| Метод | Локальный (`Local.IPlayerEvents`) | Глобальный (`Global.IPlayerEvents`) |
|---|---|---|
| Спавн | `OnSpawned()` | `OnPlayerSpawned( player )` |
| Смерть | `OnDied( PlayerDiedParams )` | `OnPlayerDied( player, PlayerDiedParams )` |
| Урон (info) | `OnDamage( PlayerDamageParams )` | `OnPlayerDamage( player, PlayerDamageParams )` |
| Прыжок | `OnJump()` | `OnPlayerJumped( player )` |
| Приземление | `OnLand( distance, velocity )` | `OnPlayerLanded( player, distance, velocity )` |
| Самоубийство | `OnSuicide()` | `OnPlayerSuicide( player )` |
| Подбор оружия | `OnPickup( e )` | `OnPlayerPickup( e )` |
| Выброс оружия | `OnDrop( e )` | `OnPlayerDrop( e )` |
| Смена оружия | `OnSwitchWeapon( e )` | `OnPlayerSwitchWeapon( e )` |
| Удаление оружия | `OnRemoveWeapon( e )` | `OnPlayerRemoveWeapon( e )` |
| Перенос слотов | `OnMoveSlot( e )` | `OnPlayerMoveSlot( e )` |
| Pre-damage | `OnDamaging( PlayerDamageEvent )` | `OnPlayerDamaging( PlayerDamageEvent )` |
| Pre-respawn | — | `OnPlayerRespawning( PlayerRespawnEvent )` |
| Kill (по другому) | `OnKill( PlayerKillEvent )` | `OnPlayerKill( PlayerKillEvent )` |

### События камеры (только `Local`)

```csharp
void OnCameraMove( ref Angles angles ) { }
void OnCameraSetup( CameraComponent camera ) { }
void OnCameraPostSetup( CameraComponent camera ) { }
```

`ref Angles` — метод может **изменить** углы камеры (например, отдача оружия). `OnCameraSetup` — настройка до рендера, `OnCameraPostSetup` — после, для эффектов поверх вьюмодели.

Камера привязана к конкретному игроку, поэтому в `Global` этих событий нет.

## Пример использования

```csharp
// На GameObject игрока — реагируем только на «своего».
public sealed class Health : Component, Local.IPlayerEvents
{
    [Sync] public float Hp { get; set; } = 100;

    void Local.IPlayerEvents.OnDamage( PlayerDamageParams args )
    {
        Hp -= args.Damage;
    }
}

// На отдельном HUD-объекте в сцене — реагируем на любого игрока.
public sealed class KillFeed : Component, Global.IPlayerEvents
{
    void Global.IPlayerEvents.OnPlayerDied( Player player, PlayerDiedParams args )
    {
        Log.Info( $"{player.DisplayName} убит игроком {args.Attacker}" );
    }
}
```

## Результат

После создания этого файла:
- Компоненты на объекте игрока могут реагировать на спавн, урон, смерть, прыжок, любые действия с инвентарём и камерой через `Local.IPlayerEvents`.
- Глобальные компоненты (HUD, камера, лента убийств, статистика) подписываются на `Global.IPlayerEvents`.
- Все «отменяемые» действия (подбор/выброс/смена/удаление оружия, перенос слотов, нанесение урона, респавн) идут как `class`-события с полем `Cancelled`.
- Иммутабельные данные (урон, смерть) идут как `struct`.
- Все методы имеют реализацию по умолчанию — компонент реализует только те события, которые ему нужны.

> 📖 Базовый паттерн `ISceneEvent<T>` описан в [официальной доке Facepunch/sbox-docs](https://github.com/Facepunch/sbox-docs/) (раздел Scene → Events).

---

Следующий шаг: [02.04 — Теги урона (DamageTags)](02_04_DamageTags.md)
