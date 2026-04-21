# 26.03 — Трассировка: Sphere/Box/Sweep и фильтры

## Что мы делаем?

[26.02](26_02_Tracing_Ray.md) объяснил **луч** — бесконечно тонкую линию. На практике этого мало: пуля имеет толщину, проп при спавне имеет объём, игрок — капсулу. Здесь разбираем **объёмные трассировки** и **фильтры** — что именно должно/не должно попадаться.

## Sphere Trace — «толстый луч»

Проводит сферу заданного радиуса из A в B и сообщает первое касание:

```csharp
var tr = Scene.Trace
    .Sphere( radius: 16f, startPos, endPos )
    .Run();
```

Удобно для:

- **дробовика** — сфера толще луча, заденет цель, даже если игрок чуть «не довёл прицел»;
- **взрыва** — `Sphere( radius, center, center )` (старт = конец) — проверка «во что упирается граната»;
- **гранаты, катящейся под уклон** — толстый sweep чувствует геометрию.

## Box Trace — параллелепипед

Свип по AABB:

```csharp
var tr = Scene.Trace
    .Ray( start, end )
    .Size( new BBox( -8, 8 ) )    // куб 16×16×16 вокруг луча
    .Run();
```

Используется в Source-моделях движения игрока: «капсула 32×32×72 хочет пройти из A в B — где остановится?». В тулгане-снаппере (`SnapGrid`) тоже пригождается.

## Sweep всей модели

Можно проверить, поместится ли *вся 3D-модель* в нужном повороте/позиции:

```csharp
var tr = Scene.Trace
    .Sweep( model, fromTransform, toTransform )
    .Run();
```

Это используют **спавнеры пропов**, чтобы не заспавнить ящик внутри стены.

## Фильтрация: WithTag / WithoutTag / WithAnyTag

`Scene.Trace` понимает [теги](00_11_Tags.md). Можно отбирать по ним:

```csharp
var tr = Scene.Trace
    .Ray( from, to )
    .WithoutTags( "player", "trigger" ) // пропустить игроков и триггеры
    .WithAnyTags( "world", "solid" )    // и наткнуться только на мир/твёрдое
    .Run();
```

| Метод | Смысл |
|---|---|
| `WithTag( "x" )` | попадаем только в объекты с тегом `x` |
| `WithAnyTags( "a", "b" )` | объект должен иметь хотя бы один из перечисленных |
| `WithAllTags( "a", "b" )` | объект обязан иметь оба тега одновременно |
| `WithoutTags( "x", "y" )` | пропускаем всё, у чего есть `x` ИЛИ `y` |

> ⚠️ Не путай `WithTag` (требование) и `WithoutTag` (исключение). Это разная логика.

## Игнор конкретного объекта

Когда автомобиль стреляет ракетой, ракета не должна попадать ни в водителя, ни в кузов. Игнорируем целую иерархию:

```csharp
var tr = Scene.Trace
    .Ray( muzzle, muzzle + dir * 8192f )
    .IgnoreGameObjectHierarchy( vehicle.GameObject )
    .Run();
```

Можно игнорировать список:

```csharp
.IgnoreGameObject( go1 )
.IgnoreGameObject( go2 )
```

## Многократные попадания (RunAll)

`Run()` возвращает **первое** попадание. Если нужно знать **все** объекты, через которые прошёл луч (например, пробивная пуля сквозь несколько целей):

```csharp
var hits = Scene.Trace
    .Ray( from, to )
    .RunAll();    // возвращает SceneTraceResult[]
```

> Каждый элемент массива — отдельное попадание, **в порядке от ближайшего к дальнему**.

## Полезные сценарии

### 1. «Можно ли встать в этой точке?» (для спавна игрока)

```csharp
var trace = Scene.Trace
    .Ray( spawnPos + Vector3.Up * 4, spawnPos + Vector3.Up * 4 )
    .Size( PlayerHull )           // BBox размером с игрока
    .WithoutTags( "trigger" )
    .Run();

bool isClear = !trace.StartedSolid;  // не зажат ли в геометрии
```

### 2. «Что под прицелом мыши?» (для тулгана)

```csharp
var ray = Camera.ScreenPixelToRay( Mouse.Position );
var tr  = Scene.Trace.Ray( ray, 8192f )
    .IgnoreGameObjectHierarchy( Player.GameObject )
    .Run();
```

### 3. «Кто попал в радиус взрыва?»

```csharp
foreach ( var hit in Scene.Trace
    .Sphere( radius: 256f, center, center )
    .RunAll() )
{
    if ( hit.GameObject?.Components.Get<HealthComponent>() is { } hc )
        hc.TakeDamage( ... );
}
```

(Хотя для радиус-урона есть и более прямой `Scene.FindInPhysics( BBox )`, но trace тоже подходит.)

## `StartedSolid` и `StartedInsideSolid`

Если старт трассировки **уже внутри** какого-то коллайдера, флаг `StartedSolid` будет `true`. Это сигнал: «игрок зажат в стене», «бочка спавнится в полу». Хороший спавнер всегда проверяет это поле.

## Что важно запомнить

- **Sphere/Box/Sweep** = объёмная трассировка вместо тонкой линии.
- **`Size( BBox )`** превращает `.Ray()` в коробочный свип.
- **`WithTag/WithoutTag/WithCollisionRules`** — фильтры по тегам и матрице столкновений.
- **`IgnoreGameObjectHierarchy(self)`** обязателен, чтобы не попасть в свои же коллайдеры.
- **`RunAll()`** возвращает все попадания, отсортированные по дистанции.
- **`StartedSolid`** — индикатор «старт внутри геометрии».

## Что дальше?

В [26.04](26_04_Physics_Events.md) разберём **физические события**: как реагировать на столкновение, не запуская трассировку каждый кадр.
