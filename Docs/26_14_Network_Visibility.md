# 26.14 — Сетевая видимость (Network Visibility)

## Что мы делаем?

По умолчанию **каждый сетевой объект транслируется каждому игроку**, всё время. Это нормально для маленьких сцен (сам Sandbox), но в больших мирах (RP-сервера, MMO-подобное) — катастрофа: 200 игроков × 5000 объектов = миллион обновлений в секунду. **Network Visibility** решает: «этот объект игроку Б видеть не нужно, не отправляй».

## Флаг `AlwaysTransmit`

У каждого сетевого объекта есть флаг `AlwaysTransmit`. По умолчанию — `true`: объект всегда виден, не отсекается. Чтобы включить отсечение, отключи его (в инспекторе или коде):

```csharp
GameObject.Network.AlwaysTransmit = false;
```

После этого решение «видеть или нет» принимается отдельно для каждой пары *(объект, игрок)*.

## `INetworkVisible` — контролёр видимости

Реализуется на компоненте, который сидит на **корневом** GameObject сетевого объекта:

```csharp
public sealed class DistanceBasedVisibility : Component, Component.INetworkVisible
{
    [Property] public float MaxDistance { get; set; } = 4096f;

    public bool IsVisibleToConnection( Connection connection, in BBox worldBounds )
    {
        var playerObj = connection.Pawn?.GameObject;
        if ( playerObj == null ) return true;

        float dist = worldBounds.Center.Distance( playerObj.WorldPosition );
        return dist <= MaxDistance;
    }
}
```

| Параметр | Что это |
|---|---|
| `Connection connection` | игрок, для которого мы решаем |
| `BBox worldBounds` | объёмный AABB нашего объекта в мире |

Метод вызывается **только у владельца** (того, кто транслирует объект).

## Что значит «не видно»

Если `IsVisibleToConnection` вернул `false`:

| Что прекращается | Что продолжается |
|---|---|
| Sync Var-обновления | RPC всё ещё доходят |
| Transform-обновления | Объект **не уничтожается** на клиенте |
| | На клиенте объект становится **Disabled**, ждёт |

Когда видимость снова станет `true`, объект «оживёт» и продолжит обновляться.

## PVS-fallback на Hammer-картах

Если **на корне нет** `INetworkVisible`-компонента, **но карта собрана с VIS** в Hammer, движок сам отсечёт объекты по PVS (Potentially Visible Set). Для статичных детальных миров (RP-карты) это работает «бесплатно».

## Идиомы

### 1. Дистанция + frustum

```csharp
public bool IsVisibleToConnection( Connection conn, in BBox bounds )
{
    var pawn = conn.Pawn?.GameObject;
    if ( pawn == null ) return true;

    var dir = bounds.Center - pawn.WorldPosition;
    if ( dir.LengthSquared > 4096f * 4096f ) return false;
    return true;
}
```

### 2. Только своей команде

```csharp
public bool IsVisibleToConnection( Connection conn, in BBox bounds )
{
    var ownerPlayer = Network.Owner?.Pawn as Player;
    var targetPlayer = conn.Pawn as Player;
    return ownerPlayer?.TeamId == targetPlayer?.TeamId;
}
```

### 3. Скрыть от мёртвых

```csharp
public bool IsVisibleToConnection( Connection conn, in BBox bounds )
{
    var p = conn.Pawn as Player;
    return p?.IsAlive ?? true;
}
```

## Стоимость

`IsVisibleToConnection` вызывается:

- для каждой пары *(объект без AlwaysTransmit, игрок)*;
- ~раз в физический шаг.

Делай его **дешёвым**:

- сравнения дистанций — через `LengthSquared`, без `sqrt`;
- кеши «лагерь/команда» — храни поле и не вычисляй каждый раз;
- никаких `Scene.FindAll`, `Components.Get` внутри.

## Что **не** надо делать

- ❌ Не ставить `AlwaysTransmit = false` всему подряд. Простому игроку (1v1, deathmatch) это даст лагов больше, чем экономии.
- ❌ Не делать `INetworkVisible` асимметричным («А не видит Б, но Б видит А, и они стреляют»). Если асимметрия нужна — заверни в правила и убедись, что игроки понимают.
- ❌ Не пытаться через видимость сделать «античит» — клиент уже знает об объекте, просто не получает апдейтов. Реальные секреты держи на сервере.

## Что важно запомнить

- По умолчанию `AlwaysTransmit = true` — все видят всех.
- `INetworkVisible` на **корне** сетевого объекта решает, кому отправлять обновления.
- Если объект «не виден», RPC всё ещё ходят, объект становится `Disabled` локально, не удаляется.
- На Hammer-картах при отсутствии `INetworkVisible` работает PVS-fallback.
- Метод вызывается часто — держи его быстрым.

## Что дальше?

В [26.15](26_15_Localization.md) — локализация UI: токены `#…`, словари по языкам.
