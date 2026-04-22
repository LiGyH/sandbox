# 27.07 — Сетевые паттерны: prediction, lag-comp, interest management

## Что мы делаем?

Берём «кубики» из [27.06](27_06_Network_Internals.md) (snapshot, RPC, [Sync], visibility) и строим из них **готовые архитектурные паттерны**, которые применяются в реальных мультиплеер-играх. Этот этап — справочник: для каждого паттерна — суть, когда использовать, как реализовать в s&box, типовые ловушки.

## Паттерн 1. Server-Authoritative (серверный авторитет)

**Суть.** Истинное состояние мира хранится **только** на сервере. Клиент шлёт «намерения» (`Input`), сервер их проверяет и отдаёт результат. Это **базовый** паттерн в s&box и почти любой современной мультиплеер-игре.

**Когда нужен.** Везде, где есть PvP, экономика, инвентарь, читы.

**Как в s&box.**

```csharp
public partial class Player : Component
{
    [Sync( SyncFlags.FromHost )] public Vector3 ServerPosition { get; set; }
    [Sync( SyncFlags.FromHost )] public int Health { get; set; } = 100;

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;

        // Клиент шлёт ввод
        var wish = BuildWishVelocity();
        SendInputToServer( wish );
    }

    [Rpc.Host]
    private void SendInputToServer( Vector3 wishVelocity )
    {
        // Только сервер двигает «истинного» игрока
        Validate( ref wishVelocity );
        Move( wishVelocity );
        ServerPosition = WorldPosition;
    }
}
```

**Ловушки.**

- ❌ Не делай `Health -= damage` на клиенте «для красоты UI» — UI обновится через `[Sync]` сам, не дублируй.
- ❌ Каждый `[Rpc.Host]` — RTT туда + обратно. Без prediction (см. ниже) ход будет «лагать».

---

## Паттерн 2. Client-Side Prediction (клиентское предсказание)

**Суть.** Чтобы убрать лаг между «нажал W» и «начал двигаться», клиент **предсказывает** результат своего ввода **локально** (своей копией физики), а сервер **позже** подтвердит или опровергнет.

**Когда нужен.** Любой шутер, экшен, MOBA. RP/MMO — обычно нет, там и так медленно.

**Как в s&box.**

```csharp
protected override void OnFixedUpdate()
{
    if ( IsProxy ) return;

    var wish = BuildWishVelocity();

    // Локально (предсказание)
    Move( wish );

    // На сервер (для проверки)
    SendInputToServer( wish );
}
```

И тут начинается главное: **что если сервер посчитал иначе?**

---

## Паттерн 3. Server Reconciliation (сверка)

**Суть.** Клиент периодически получает «настоящую» позицию от сервера ([Sync]). Если она расходится с локальной — клиент **мягко** сдвигается к ней, не телепортируясь.

**Как в s&box.**

```csharp
[Sync( SyncFlags.FromHost, SyncFlags.Interpolate )]
public Vector3 ServerPosition { get; set; }

protected override void OnUpdate()
{
    if ( IsProxy ) return;

    // Расхождение с серверной правдой
    var error = ServerPosition - WorldPosition;
    if ( error.Length > 100f )
    {
        // Жёстко: телепорт (антирастяжка после читерства)
        WorldPosition = ServerPosition;
    }
    else
    {
        // Мягко: подтянуть на 10% за кадр
        WorldPosition += error * 0.1f;
    }
}
```

**Ловушки.**

- ❌ Если сверка слишком жёсткая — игрок будет «дёргаться».
- ❌ Если слишком мягкая — читер сможет «уйти» от сервера на минуту.
- ✅ Подбирай порог (100 ед. в примере) и коэффициент (0.1) под жанр.

---

## Паттерн 4. Lag Compensation (компенсация лага попадания)

**Суть.** Игрок с пингом 100 мс стреляет «по голове противника, который **виделся** ему на этой позиции 100 мс назад». Если на сервере просто проверять «попал по текущей позиции» — стрелок никогда не попадёт. Сервер **откатывает мир на пинг назад**, проверяет попадание, потом откатывает обратно.

**Когда нужен.** Шутеры, особенно с быстрым TTK. В RP/MMO — почти никогда.

**Как в s&box.** Из коробки в s&box нет встроенной lag-comp, делается руками: сервер хранит **историю позиций** игроков (например, последние 500 мс) и на момент выстрела трассирует против неё.

```csharp
public partial class Player
{
    private struct PositionRecord { public float Time; public Vector3 Pos; }
    private readonly List<PositionRecord> _history = new();
    private const float HistorySeconds = 0.5f;

    protected override void OnFixedUpdate()
    {
        if ( !Networking.IsHost ) return;

        _history.Add( new PositionRecord { Time = Time.Now, Pos = WorldPosition } );
        _history.RemoveAll( r => Time.Now - r.Time > HistorySeconds );
    }

    public Vector3 GetPositionAt( float t )
    {
        // Простейшая ближайшая запись; в проде — линейная интерполяция между двумя
        var rec = _history.OrderBy( r => MathF.Abs( r.Time - t ) ).FirstOrDefault();
        return rec.Pos;
    }
}

[Rpc.Host]
public void Fire( Ray ray, float clientTime )
{
    var pingMs = Rpc.Caller.Ping;
    var rewindTo = Time.Now - pingMs / 1000f;

    foreach ( var p in Scene.GetAllComponents<Player>() )
        p.SetTracePosition( p.GetPositionAt( rewindTo ) );

    var hit = Scene.Trace.Ray( ray, 8192f ).Run();

    foreach ( var p in Scene.GetAllComponents<Player>() )
        p.RestoreTracePosition();

    if ( hit.GameObject?.Components.Get<Player>() is Player victim )
        victim.TakeDamage( 25, Rpc.Caller );
}
```

**Ловушки.**

- ❌ Lag-comp создаёт «выстрел из-за угла»: жертва уже спряталась, но стрелок «попал». Это нормальная плата.
- ❌ Без ограничения: при пинге 1000 мс игрок может стрелять «в прошлое на секунду». Ставь cap на `pingMs`.

---

## Паттерн 5. Interest Management / Area Of Interest

**Суть.** Не отправлять игроку информацию об объектах, до которых **далеко** или которые **не должны быть видны**. На сервере — сетка/октодерево/PVS/zones.

**Когда нужен.** От 64 игроков и больше. Без него на 256 — труба.

**Базовая реализация в s&box** — `INetworkVisible` ([26.14](26_14_Network_Visibility.md)):

```csharp
public sealed class AreaOfInterest : Component, Component.INetworkVisible
{
    public const float RangeSq = 8192f * 8192f;

    public bool IsVisibleToConnection( Connection c, in BBox bounds )
    {
        var pawnPos = (c.Pawn?.GameObject)?.WorldPosition ?? default;
        return bounds.Center.DistanceSquared( pawnPos ) <= RangeSq;
    }
}
```

**Усложнения для крупных миров (RP/MMO):**

- **Грид-зоны.** Разбей мир на ячейки 256×256 м. Игрок в ячейке (i,j) видит объекты из (i±1, j±1) — 9 ячеек. Дешевле, чем перебирать всех попарно.
- **Октодерево.** Для трёхмерных миров. В s&box можно сделать на `Dictionary<(int,int,int), List<GameObject>>`.
- **PVS на Hammer-картах.** Бесплатный «рентген» статической геометрии (см. [26.14](26_14_Network_Visibility.md)).
- **Кадровое распределение.** Не вызывай `IsVisibleToConnection` для всех объектов в каждом tick — раздели по группам, проверяй 1/4 объектов за tick.

**Ловушки.**

- ❌ «Поп-ин» при движении: игрок забегает в зону — объекты «всплывают». Решается **мягким fade-in** на клиенте + расширенным радиусом «пред-загрузки».
- ❌ Дорогая `IsVisibleToConnection` — она вызывается **очень** часто. Никаких `Scene.FindAll` внутри ([26.14](26_14_Network_Visibility.md)).

---

## Паттерн 6. Tick-based Lockstep

**Суть.** Никаких snapshot-ов. Все клиенты получают **тот же список инпутов** за tick и **сами** считают физику. Применяется в RTS/файтингах.

**В s&box** — нестандартный путь, придётся:

- Отключить sync transform-ов сетевых объектов.
- Через RPC рассылать список инпутов по tick-у.
- Гарантировать **детерминизм** физики (а вот это сложно — Source 2 физика не гарантированно детерминирована).

**Когда нужен.** Сильно специфические случаи (StarCraft-like RTS на 1000+ юнитов). Для обычной игры — **нет**.

---

## Паттерн 7. Snapshot Interpolation (для прокси-объектов)

**Суть.** То, что клиент видит чужого игрока **на 100–200 мс позади** «настоящего», и интерполирует между двумя последними snapshot-ами. Это уже встроено в s&box (см. [27.06](27_06_Network_Internals.md)) — не надо ничего делать руками. Но **знать** надо: это объясняет «странные» эффекты в lag-comp и почему «свой» игрок дёргается реже, чем «чужие».

---

## Паттерн 8. Authoritative Movement через ConCmd

**Суть (упрощение #1).** Не пиши свою lag-comp и prediction — отдай движение клиенту через ownership и просто [Sync] позицию. Это **не** server-authoritative, **читы возможны**, но проще на порядок.

**Когда применять.** Co-op (4 человека, всем доверяем), сэндбокс, ПвЕ-крафт.

```csharp
// Игрок ВЛАДЕЕТ своим аватаром -> двигает локально
// Сервер только реплицирует
[Sync] public Vector3 SyncPosition { get; set; }

protected override void OnFixedUpdate()
{
    if ( IsProxy ) return;
    Move( BuildWishVelocity() );
    SyncPosition = WorldPosition;
}
```

Это то, как работает базовый `NetworkHelper` и большинство примеров sbox.game. Для PvP **не подходит**.

---

## Паттерн 9. Event Sourcing для критичных действий

**Суть.** Не ставь «состояние» в `[Sync]`, а гоняй **журнал событий**: «выпил зелье в 12:34», «купил меч за 100 монет». Сервер пересчитывает итог по событиям, БД хранит лог. Удобно для RP с экономикой и спорами «он у меня украл!».

**Реализация в s&box.** Свой `[Rpc.Host]` API:

```csharp
[Rpc.Host]
public void RequestPurchase( int itemId )
{
    var actor = Rpc.Caller;
    if ( !EconomyService.TryBuy( actor.SteamId, itemId, out var price ) ) return;

    Log.Info( $"[Eco] {actor.DisplayName} bought {itemId} for {price}" );
    Database.LogTransactionAsync( actor.SteamId, itemId, price ).Forget();
}
```

И `EconomyService` — сервис, считающий состояние из событий.

---

## Сводная таблица: какой паттерн под какой жанр

| Жанр | Authoritative | Prediction | Lag-Comp | Interest Mgmt |
|---|:-:|:-:|:-:|:-:|
| Co-op (4 ч.) sandbox | желателен | нет | нет | нет |
| Deathmatch (16) | **да** | **да** | **да** | нет |
| Battle Royale (64–128) | **да** | **да** | **да** | **да** |
| MOBA (10) | **да** | **да** | желательно | нет |
| RP-сервер (200) | **да** | нет | нет | **да** |
| MMO (1000+) | **да** | нет | нет | **да + грид** |
| RTS lockstep | особый случай — Lockstep | — | — | — |

Подробнее по числам и архитектурам — [27.08](27_08_Scaling_Players.md) и [27.09](27_09_Genre_Examples.md).

## Что важно запомнить

- **Server-authoritative** — база. Без неё PvP-игра неприемлема.
- **Prediction + reconciliation** — пара, без которой ввод «лагает».
- **Lag-compensation** — обязательна для шутеров с пингом 50+.
- **Interest management** — рычаг масштабирования; от 64 игроков критично.
- Не все паттерны нужны всем. Не «вкручивай» lag-comp в RP-сервер.

## Что дальше?

В [27.08](27_08_Scaling_Players.md) — переходим от «как делать» к «сколько игроков можно вытянуть»: профили под 16, 64, 128, 256, 1000 и 1000+, с конкретными рекомендациями по каждому пункту.
