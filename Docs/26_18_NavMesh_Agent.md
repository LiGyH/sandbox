# 26.18 — NavMeshAgent: движение по навмешу

## Что мы делаем?

`NavMeshAgent` — компонент, который **сам водит** GameObject по [навмешу](26_17_NavMesh.md). Ставишь его на NPC, говоришь `MoveTo(point)` — агент ищет путь, обходит препятствия, обходит других агентов, останавливается в нужной точке.

## Подключение

1. На GameObject NPC добавь компонент `NavMeshAgent`.
2. Задай в инспекторе:
   - **MaxSpeed** — максимальная скорость (units/sec, обычно 100–250).
   - **Acceleration** — насколько резко разгоняется/тормозит.
   - **Radius** — радиус столкновения с другими агентами (для обхода толпы).
   - **Height** — высота капсулы (для проверки вертикальных препятствий).

Агент сам обновляет `WorldPosition` и `WorldRotation` GameObject'а — твой NPC двигается.

## Базовое API

```csharp
NavMeshAgent agent = Components.Get<NavMeshAgent>();

agent.MoveTo( targetPosition );   // идти к точке
agent.Stop();                     // прекратить идти
var v = agent.Velocity;           // текущая скорость (Vector3)
```

`MoveTo` **не блокирующий** — агент будет идти, пока не дойдёт или пока ему не сказали `Stop()`. Каждый раз, когда ты вызываешь `MoveTo` с новой точкой, путь пересчитывается.

## Связь с обычной AI-логикой

Обычно у тебя на NPC есть **два** компонента:

1. `NavMeshAgent` — *двигает*.
2. Твой собственный `EnemyAI`-компонент (часть Phase 22) — *решает, куда*.

```csharp
public sealed class EnemyAI : Component
{
    [RequireComponent] public NavMeshAgent Agent { get; set; }

    private GameObject _target;

    protected override void OnFixedUpdate()
    {
        if ( _target.IsValid() )
        {
            Agent.MoveTo( _target.WorldPosition );
            Agent.MaxSpeed = 220;          // бежим
        }
        else
        {
            Agent.Stop();
            Agent.MaxSpeed = 100;
        }
    }
}
```

## Анимация по `Velocity`

Самая частая ошибка: «NPC идёт ногами в воздухе, скользит». Решение — **анимировать модель по `agent.Velocity`**:

```csharp
var anim = Components.Get<SkinnedModelRenderer>();
anim.Set( "move_x", agent.Velocity.x );
anim.Set( "move_y", agent.Velocity.y );
anim.Set( "speed",  agent.Velocity.WithZ(0).Length );
```

Параметры зависят от твоей анимационной графы; смысл один: скорость → скорость анимации ходьбы.

## Несколько целей подряд (патрулирование)

```csharp
private Vector3[] _waypoints;
private int _wpIndex;

protected override void OnFixedUpdate()
{
    if ( !Agent.MoveTo( _waypoints[_wpIndex] ) )
    {
        // путь не найден — пропустить
        _wpIndex = (_wpIndex + 1) % _waypoints.Length;
        return;
    }

    if ( WorldPosition.Distance( _waypoints[_wpIndex] ) < 32f )
        _wpIndex = (_wpIndex + 1) % _waypoints.Length;
}
```

(Если `MoveTo` возвращает `bool`, пользуйся им; иначе — проверяй дистанцию вручную, как в примере выше.)

## Поиск ближайшей точки на навмеше

NPC в воздухе или внутри препятствия = плохая исходная позиция. Перед `MoveTo` приземли точку на навмеш:

```csharp
if ( Scene.NavMesh.GetClosestPoint( target ) is Vector3 onMesh )
    Agent.MoveTo( onMesh );
```

(точное имя API зависит от версии — проверяй актуальный `Scene.NavMesh`).

## Регулировка под ситуацию

Меняй `MaxSpeed` и `Acceleration` на лету:

```csharp
Agent.MaxSpeed     = isAlerted ? 280 : 80;
Agent.Acceleration = isAlerted ? 1200 : 400;
```

Это даёт «бег при тревоге» и «прогулку в покое» без дополнительных контроллеров.

## Толпа (crowd avoidance)

Несколько агентов друг друга «расталкивают» — это встроенное поведение. На карте с 50 NPC они будут обтекать препятствия и друг друга, а не «вязнуть в кучу». Для тяжёлых сцен можно отключать `Agent.AvoidAgents`, чтобы съэкономить CPU (тогда они будут проходить друг сквозь друга).

## Когда НЕ использовать NavMeshAgent

- Игрок (его двигает `PlayerController`/`CharacterController`, а не AI).
- Падающие предметы (это `Rigidbody`, физика).
- Быстро меняющиеся точки (раз в кадр) — путь пересчитывается, дорого. Лучше задавай цель раз в 0.1–0.5 сек.

## Что важно запомнить

- `NavMeshAgent` — сам двигает GameObject; ты задаёшь только цель.
- `MoveTo(point)` идёт, `Stop()` останавливает, `Velocity` — реальная скорость.
- Анимируй по `Velocity`, чтобы не было «скольжения».
- Перед `MoveTo` приземляй точку на навмеш через `GetClosestPoint`.
- Меняй `MaxSpeed` / `Acceleration` для смены поведения.
- Не дёргай `MoveTo` каждый кадр — раз в 0.1–0.5 сек хватает.

## Что дальше?

В [26.19](26_19_Terrain.md) — система рельефа Terrain.
