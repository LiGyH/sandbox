# 27.09 — Архитектуры под жанры (FPS, BR, MOBA, RP, MMO-light, симулятор)

## Что мы делаем?

Берём паттерны ([27.07](27_07_Networking_Patterns.md)) и профили нагрузки ([27.08](27_08_Scaling_Players.md)) и собираем **готовые эскизы** под 6 типичных жанров. Для каждого: целевое число игроков, ключевые сетевые объекты, какие паттерны включены, чего избегать.

## Жанр 1. Deathmatch / TDM (16–32 игрока)

**Цель.** Быстрый шутер «зашёл-стрельнул», без экономики и сейв-стейтов.

**Архитектура.**

```
sbox-server (1 экземпляр)
 ├── GameMode: DeathmatchGameMode    ← timer, scoreboard, win condition
 ├── Player x N                       ← server-authoritative + prediction
 ├── Weapon (worldmodel) x N          ← ownership на игроке
 ├── Pickup (HP, ammo) x ~30          ← respawn timer на сервере
 └── Map: garry.cs_test или своя
```

**Сетевые правила.**

- `[Sync] Health, Ammo, ScoreKills` — на каждом игроке.
- `[Rpc.Broadcast]` для эффектов выстрела (звук, искра).
- Lag-comp ([27.07](27_07_Networking_Patterns.md), паттерн 4) — **обязательна**, без неё хитрегистрация мёртвая.
- `INetworkVisible` — **не** надо: 32 игрока в одной комнате.

**Что НЕ делать.**

- ❌ Не давай клиенту менять свой `Health` напрямую.
- ❌ Не клади `[Sync]` на «временный таймер выстрела» — гоняй RPC от/к нужным сторонам.

**Профиль ([27.08](27_08_Scaling_Players.md)):** 1.

---

## Жанр 2. Battle Royale (64–128 игроков)

**Цель.** Большая карта, зона сужается, последний живой выигрывает.

**Архитектура.**

```
sbox-server (1 экземпляр на матч)
 ├── MatchManager                ← phase machine: Lobby → Drop → Play → End
 ├── ZoneController              ← синхронизированная shrinking zone
 ├── LootTable + LootSpawner     ← server-side, 5000+ предметов
 ├── Player x 128                ← server-auth + prediction + lag-comp
 ├── Vehicle x ~40               ← ownership передаётся при «сел в машину»
 └── Map: огромная, 8x8 км
```

**Сетевые правила.**

- **Interest management обязателен** — 128 игроков не должны видеть друг друга на 8 км дистанции.
  - `INetworkVisible` на пропах с радиусом 500 м.
  - `INetworkVisible` на игроках с радиусом 800 м (звук выстрела всё равно бы был слышен).
- Loot спавнится **всему миру**, но `AlwaysTransmit = false` + чанк-видимость.
- `ZoneController` — единственный «всегда видимый» объект.
- Custom snapshot data для траекторий пуль (компактнее, чем `[Sync]` на projectile).

**Шардинг.** Один матч = один сервер. Между матчами — лобби-сервер (отдельный микросервис, не sbox).

**Профиль ([27.08](27_08_Scaling_Players.md)):** 3.

---

## Жанр 3. MOBA (10 игроков, но требования к честности максимальные)

**Цель.** 5×5, фиксированная карта, экономика внутри матча, рейтинг — снаружи.

**Архитектура.**

```
sbox-server (1 экземпляр на матч, ровно 10 игроков)
 ├── MatchManager (BO1, ranking, pause)
 ├── Lane x 3 + Jungle
 ├── Tower x ~12          ← статичные [Sync]-объекты
 ├── Creep x ~50/wave     ← server-spawned NPC
 ├── Hero x 10            ← server-auth + prediction + lag-comp
 └── Item shop            ← через [Rpc.Host], валидация серверная
```

**Сетевые правила.**

- Анти-чит — параноидальный. **Всё** через `[ConCmd.Server]` + `Connection.HasPermission`-эквиваленты (см. [27.05](27_05_User_Permissions.md)) или специальные «капабилити».
- Каждый клик абилки шлёт RPC + клиент **сразу проигрывает анимацию** (prediction), сервер подтверждает или откатывает (reconciliation).
- Замедления, корни, оглушения — серверно, клиент только показывает.
- Записывай **полный demo матча** для разбора (см. [01.11](01_11_DemoRecording.md)).

**Что вне sbox.** Рейтинговая система, сезонный сброс, профиль игрока — отдельный API. Сервер только проверяет «по матчу всё ок» и шлёт результат.

**Профиль ([27.08](27_08_Scaling_Players.md)):** 1 (всего 10 игроков, но «жёсткий»).

---

## Жанр 4. RP-сервер (100–250 игроков, открытый мир)

**Цель.** Постоянный мир, экономика, профессии, машины, дома, инвентарь.

**Архитектура.**

```
sbox-server (постоянный, 24/7)
 ├── EconomyService.Server.cs      ← в #if SERVER, БД-запросы (см. 27.04)
 ├── InventoryService.Server.cs
 ├── HousingService.Server.cs
 ├── Player x ~250                  ← prediction; lag-comp НЕ нужна (нет шутерного PvP)
 ├── PropOwnable x 5000+            ← INetworkVisible обязательно (см. 26.14)
 ├── Vehicle x 200                  ← ownership на водителе
 └── Map: огромная, может быть 16x16 км или несколько сшитых
```

**Сетевые правила.**

- Жёсткое interest management: грид 200×200 м, видимость 9 ячеек.
- `UpdateRate = 20` (медленный мир, экономия канала).
- Анти-чит на критичные действия: `give_money`, `spawn_vehicle` — только админам ([27.05](27_05_User_Permissions.md)).
- Все «профессии» (полицейский, бандит) — через серверные **claim-ы** в `users/config.json` или серверный rank-сервис.
- Чат — отдельный сервис ([26.13](26_13_WebSockets.md)) или `[Rpc.Broadcast]` с фильтрацией.

**Persistent data.** Внешняя БД обязательна. Игрок «вышел/зашёл» — **загрузка/сохранение** через HTTP ([26.12](26_12_Http_Requests.md)) или прямой драйвер БД в `#if SERVER` ([27.04](27_04_Local_Project_Serverside_Code.md)).

**Профиль ([27.08](27_08_Scaling_Players.md)):** 4.

---

## Жанр 5. MMO-light / большой социальный (500–1000)

**Цель.** Постоянный мир, миграции игроков между «зонами», крупные ивенты.

**Архитектура.**

```
                ┌──────────────┐
                │  Login API   │   (HTTP, не sbox)
                └──────┬───────┘
                       │
   ┌───────────────────┼───────────────────┐
┌──▼──┐  ┌──▼──┐  ┌──▼──┐  ┌──▼──┐
│ Sh1 │  │ Sh2 │  │ Sh3 │  │ Sh4 │     каждый шард ≈ 250 игроков,
│sbox │  │sbox │  │sbox │  │sbox │     своя зона мира
└──┬──┘  └──┬──┘  └──┬──┘  └──┬──┘
   └────────┴────────┴────────┘
                │
        ┌───────▼───────┐
        │  PostgreSQL   │  ← инвентари, профили, экономика
        └───────────────┘
        ┌───────────────┐
        │     Redis     │  ← глобальный чат, кеш ценников
        └───────────────┘
```

**Сетевые правила.**

- На каждый шард — те же правила, что в RP (см. выше).
- Перемещение игрока между шардами = `Kick` с одного и `connect` ко второму с тем же SteamId; персонаж достаётся из БД.
- **Глобальные системы** (чат, торги, гильдии) — **вне** sbox-серверов, через WebSocket.
- Воркеры (агрегация рейтингов, цикл «дня») — отдельные .NET-сервисы.

**Что почти невозможно.**

- Бесшовное «1000 человек видят друг друга». В s&box нереалистично — это **дизайн** (зональный, не open-seamless).

**Профиль ([27.08](27_08_Scaling_Players.md)):** 5.

---

## Жанр 6. Социальный симулятор / сэндбокс с физикой (50–100)

**Цель.** Garry's-Mod-подобное, но крупнее: физика, инструменты, контест-сборки.

**Архитектура.**

```
sbox-server (1 экземпляр)
 ├── PhysGun, Toolgun (Фазы 8–9)
 ├── PropOwnable x 500+         ← у каждого игрока свой лимит (см. 27.03)
 ├── Constraint x 1000+         ← weld, rope, slider
 ├── SaveSystem (Фаза 23)       ← периодический бэкап на диск
 └── Map: standard sandbox map
```

**Сетевые правила.**

- Всё, что игрок ставит/спавнит, — **сначала RPC к серверу** с проверкой лимита (см. [27.05](27_05_User_Permissions.md)).
- Физика тяжёлая: ставь `PhysicsSubsteps` повыше, но следи за `Time.FixedDelta`.
- Ownership на «несомых» объектах временно передаётся клиенту — снижает лаг физики.
- `INetworkVisible` для пропов из дальних углов карты.

**Профиль ([27.08](27_08_Scaling_Players.md)):** 2 или 3.

---

## Сводная таблица «жанр → выбор»

| Жанр | Игр. | Auth | Pred | Lag-c | Vis | Шарды | БД |
|---|---:|:-:|:-:|:-:|:-:|:-:|:-:|
| Deathmatch / TDM | 32 | ✓ | ✓ | ✓ | – | – | – |
| Battle Royale | 128 | ✓ | ✓ | ✓ | ✓ | – | (статы) |
| MOBA | 10 | ✓ | ✓ | ✓ | – | – | (рейтинг) |
| RP | 250 | ✓ | (✓) | – | ✓ | – | ✓ |
| MMO-light | 1000 | ✓ | – | – | ✓ | ✓ | ✓ |
| Sandbox | 100 | (✓) | – | – | ✓ | – | (сейвы) |

## Универсальные эскизы кода

### Базовая `GameModeManager` для матчей

```csharp
public partial class GameModeManager : Component, Component.INetworkListener
{
    [Property] public float MatchSeconds { get; set; } = 600f;

    [Sync( SyncFlags.FromHost )] public float TimeLeft { get; set; }
    [Sync( SyncFlags.FromHost )] public bool MatchActive { get; set; }

    void INetworkListener.OnActive( Connection c )
    {
        if ( !MatchActive && Connection.All.Count() >= 2 )
            StartMatch();
    }

    private void StartMatch()
    {
        if ( !Networking.IsHost ) return;
        MatchActive = true;
        TimeLeft = MatchSeconds;
    }

    protected override void OnFixedUpdate()
    {
        if ( !Networking.IsHost || !MatchActive ) return;
        TimeLeft -= Time.Delta;
        if ( TimeLeft <= 0 ) EndMatch();
    }

    private void EndMatch() { /* подсчёт, ChangeMap, статы в БД */ }
}
```

Этот скелет подходит для DM/TDM/MOBA/BR — отличается только содержимое `EndMatch`.

### Базовая `WorldGrid` для interest management

```csharp
public sealed class WorldGrid : Component
{
    public const float CellSize = 256f;
    private readonly Dictionary<(int, int), HashSet<GameObject>> _cells = new();

    public (int, int) ToCell( Vector3 pos )
        => ((int)( pos.x / CellSize ), (int)( pos.y / CellSize ));

    public IEnumerable<GameObject> Around( Vector3 pos, int radiusCells = 1 )
    {
        var (cx, cy) = ToCell( pos );
        for ( int dx = -radiusCells; dx <= radiusCells; dx++ )
        for ( int dy = -radiusCells; dy <= radiusCells; dy++ )
            if ( _cells.TryGetValue( (cx + dx, cy + dy), out var set ) )
                foreach ( var go in set ) yield return go;
    }
    // + методы регистрации/снятия с регистрации объектов при движении
}
```

Используй её внутри `INetworkVisible`-компонента, чтобы быстро выбрать «соседей» вместо `Scene.FindAll`.

## Что важно запомнить

- Каждому жанру — свой набор паттернов. Не используй BR-схему в DM, не строй MMO без шардинга.
- Постоянный мир ⇒ внешняя БД и `#if SERVER`-сервисы.
- 1000+ игроков = **архитектурная** задача, а не «настройка сервера».
- На любом серьёзном жанре — обязательны: server-authoritative + visibility + админ-логи + бэкапы.

## Что дальше?

В [27.10](27_10_Hosting_Operations.md) — финальный эксплуатационный блок: где хостить, как мониторить, как обновлять без даунтайма, бэкапы, защита от DDoS и злоупотреблений.
