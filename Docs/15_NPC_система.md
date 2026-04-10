# Этап 15: NPC система

## Цель этого этапа

Создать систему NPC (Non-Player Characters) — персонажей, управляемых компьютером. NPC могут: ходить по навигационной сетке, видеть игрока, атаковать, убегать.

## Архитектура NPC

NPC построен на паттерне **«Расписание» (Schedule)** — последовательность задач:

```
NPC
├── Layers (слои — постоянно работают):
│   ├── SensesLayer    ← Видит, слышит
│   ├── AnimationLayer ← Анимации
│   └── SpeechLayer    ← Говорит фразы
│
└── Schedule (расписание — текущее поведение):
    ├── IdleSchedule      ← Стоит, смотрит по сторонам
    ├── PatrolSchedule    ← Ходит по маршруту
    ├── ChaseSchedule     ← Преследует цель
    └── FleeSchedule      ← Убегает
        └── Tasks:
            ├── MoveTo( point )    ← Иди к точке
            ├── LookAt( target )   ← Смотри на цель
            ├── FireWeapon()       ← Стреляй
            ├── Say( "phrase" )    ← Скажи фразу
            └── Wait( seconds )    ← Подожди
```

### Schedule vs Task:

- **Schedule** — «что NPC делает сейчас» (патрулирует, атакует, убегает)
- **Task** — конкретное действие внутри расписания (иди к точке, стреляй)

## Шаг 15.1: Базовый NPC

Создай файл `Code/Npcs/Npc.cs`:

```csharp
/// <summary>
/// Базовый класс NPC. Управляет навигацией, расписаниями и слоями.
/// </summary>
public partial class Npc : Component, Component.IDamageable
{
    // --- Навигация ---
    
    [RequireComponent] public NavMeshAgent Agent { get; set; }

    /// <summary>
    /// Здоровье NPC.
    /// </summary>
    [Property, Sync] public float Health { get; set; } = 100f;

    /// <summary>
    /// Цель (кого NPC преследует/атакует).
    /// </summary>
    public GameObject Target { get; set; }

    /// <summary>
    /// Текущее расписание.
    /// </summary>
    public ScheduleBase CurrentSchedule { get; private set; }

    /// <summary>
    /// Слои (постоянно работающие подсистемы).
    /// </summary>
    private List<BaseNpcLayer> _layers = new();

    // --- Инициализация ---

    protected override void OnEnabled()
    {
        // Регистрируем слои
        _layers = GetComponentsInChildren<BaseNpcLayer>().ToList();

        // Устанавливаем начальное расписание
        SetSchedule( CreateDefaultSchedule() );
    }

    /// <summary>
    /// Создаёт расписание по умолчанию (переопределяется в наследниках).
    /// </summary>
    protected virtual ScheduleBase CreateDefaultSchedule()
    {
        return null; // Наследники определяют поведение
    }

    // --- Обновление ---

    protected override void OnFixedUpdate()
    {
        if ( !Networking.IsHost ) return;

        // Обновляем слои (зрение, слух, речь)
        foreach ( var layer in _layers )
        {
            layer.OnLayerUpdate( this );
        }

        // Обновляем расписание
        CurrentSchedule?.OnScheduleUpdate( this );
    }

    // --- Навигация ---

    /// <summary>
    /// Двигаться к точке через NavMesh.
    /// </summary>
    public void MoveTo( Vector3 position )
    {
        if ( Agent.IsValid() )
        {
            Agent.MoveTo( position );
        }
    }

    /// <summary>
    /// Остановиться.
    /// </summary>
    public void StopMoving()
    {
        if ( Agent.IsValid() )
        {
            Agent.Stop();
        }
    }

    // --- Расписания ---

    /// <summary>
    /// Установить новое расписание.
    /// </summary>
    public void SetSchedule( ScheduleBase schedule )
    {
        CurrentSchedule?.OnScheduleEnd( this );
        CurrentSchedule = schedule;
        CurrentSchedule?.OnScheduleStart( this );
    }

    // --- Урон ---

    public void OnDamage( in DamageInfo dmg )
    {
        if ( Health <= 0 ) return;

        Health -= dmg.Damage;

        if ( Health <= 0 )
        {
            Die( dmg );
        }
        else
        {
            OnHurt( dmg );
        }
    }

    protected virtual void OnHurt( DamageInfo dmg )
    {
        // Наследники могут реагировать на урон
        // Например: начать атаковать того, кто ударил
        if ( dmg.Attacker.IsValid() )
        {
            Target = dmg.Attacker;
        }
    }

    protected virtual void Die( DamageInfo dmg )
    {
        // Уведомляем GameManager
        GameManager.Current?.OnNpcDeath( GameObject.Name, dmg );

        // Создаём ragdoll (как у игрока)
        CreateRagdoll();
        GameObject.Destroy();
    }

    [Rpc.Broadcast]
    private void CreateRagdoll()
    {
        var renderer = GetComponentInChildren<SkinnedModelRenderer>();
        if ( !renderer.IsValid() ) return;

        var go = new GameObject( true, "NPC Ragdoll" );
        go.Tags.Add( "ragdoll" );
        go.WorldTransform = WorldTransform;

        var body = go.AddComponent<SkinnedModelRenderer>();
        body.CopyFrom( renderer );
        body.UseAnimGraph = false;

        var physics = go.AddComponent<ModelPhysics>();
        physics.Model = body.Model;
        physics.Renderer = body;
        physics.CopyBonesFrom( renderer, true );
    }
}
```

### Как работает NavMesh:

**NavMesh** (навигационная сетка) — это невидимая «карта проходимости». Она генерируется из геометрии карты:

```
Карта сверху:                NavMesh:
┌────────────────┐           ┌────────────────┐
│   ████         │           │   ████         │
│   ████  ████   │           │   ████  ████   │
│         ████   │    →      │░░░░░░░░░████   │
│                │           │░░░░░░░░░░░░░░░░│
│   ████         │           │░░░████░░░░░░░░░│
└────────────────┘           └────────────────┘
████ = стены                 ░░░ = проходимая зона

NPC использует NavMesh чтобы находить путь:
A ──→ ██ ──обход──→ B (а не сквозь стену!)
```

`NavMeshAgent` — встроенный компонент движка. Он автоматически:
- Находит путь от A до B по NavMesh
- Обходит препятствия
- Двигается с заданной скоростью

## Шаг 15.2: ScheduleBase — базовое расписание

Создай файл `Code/Npcs/ScheduleBase.cs`:

```csharp
/// <summary>
/// Базовый класс расписания NPC.
/// Расписание = последовательность задач (Tasks).
/// </summary>
public abstract class ScheduleBase
{
    protected Queue<TaskBase> Tasks { get; } = new();
    private TaskBase _currentTask;

    public virtual void OnScheduleStart( Npc npc ) { }
    public virtual void OnScheduleEnd( Npc npc ) { }

    /// <summary>
    /// Обновляется каждый тик. Выполняет текущую задачу.
    /// </summary>
    public virtual void OnScheduleUpdate( Npc npc )
    {
        // Если текущей задачи нет — берём следующую
        if ( _currentTask == null )
        {
            if ( Tasks.Count == 0 )
            {
                OnScheduleComplete( npc );
                return;
            }

            _currentTask = Tasks.Dequeue();
            _currentTask.OnTaskStart( npc );
        }

        // Обновляем текущую задачу
        var status = _currentTask.OnTaskUpdate( npc );

        if ( status != TaskStatus.Running )
        {
            _currentTask.OnTaskEnd( npc );
            _currentTask = null;
        }
    }

    /// <summary>
    /// Все задачи выполнены.
    /// </summary>
    protected virtual void OnScheduleComplete( Npc npc ) { }
}
```

## Шаг 15.3: TaskBase — базовая задача

Создай файл `Code/Npcs/TaskBase.cs`:

```csharp
/// <summary>
/// Базовая задача NPC (иди к точке, подожди, стреляй...).
/// </summary>
public abstract class TaskBase
{
    public virtual void OnTaskStart( Npc npc ) { }
    public abstract TaskStatus OnTaskUpdate( Npc npc );
    public virtual void OnTaskEnd( Npc npc ) { }
}

/// <summary>
/// Статус выполнения задачи.
/// </summary>
public enum TaskStatus
{
    Running,    // Ещё выполняется
    Success,    // Успешно завершена
    Failed      // Провалена
}
```

## Шаг 15.4: Конкретные задачи

Создай файл `Code/Npcs/Tasks/MoveTo.cs`:

```csharp
/// <summary>
/// Задача: идти к точке.
/// </summary>
public class MoveToTask : TaskBase
{
    private Vector3 _destination;
    private float _tolerance;

    public MoveToTask( Vector3 destination, float tolerance = 32f )
    {
        _destination = destination;
        _tolerance = tolerance;
    }

    public override void OnTaskStart( Npc npc )
    {
        npc.MoveTo( _destination );
    }

    public override TaskStatus OnTaskUpdate( Npc npc )
    {
        var distance = (npc.WorldPosition - _destination).Length;
        return distance < _tolerance ? TaskStatus.Success : TaskStatus.Running;
    }

    public override void OnTaskEnd( Npc npc )
    {
        npc.StopMoving();
    }
}
```

Создай файл `Code/Npcs/Tasks/Wait.cs`:

```csharp
/// <summary>
/// Задача: подождать N секунд.
/// </summary>
public class WaitTask : TaskBase
{
    private float _duration;
    private RealTimeSince _started;

    public WaitTask( float duration )
    {
        _duration = duration;
    }

    public override void OnTaskStart( Npc npc )
    {
        _started = 0;
    }

    public override TaskStatus OnTaskUpdate( Npc npc )
    {
        return _started >= _duration ? TaskStatus.Success : TaskStatus.Running;
    }
}
```

## Шаг 15.5: SensesLayer — Зрение

Создай файл `Code/Npcs/Layers/SensesLayer.cs`:

```csharp
/// <summary>
/// Слой зрения — NPC "видит" объекты перед собой.
/// </summary>
public class SensesLayer : BaseNpcLayer
{
    [Property] public float ViewDistance { get; set; } = 1024f;
    [Property] public float ViewAngle { get; set; } = 120f;

    public override void OnLayerUpdate( Npc npc )
    {
        // Ищем игроков в радиусе
        var players = Scene.GetAll<Player>();
        
        foreach ( var player in players )
        {
            var direction = (player.WorldPosition - npc.WorldPosition).Normal;
            var distance = (player.WorldPosition - npc.WorldPosition).Length;

            // Проверяем расстояние
            if ( distance > ViewDistance ) continue;

            // Проверяем угол зрения
            var angle = Vector3.GetAngle( npc.WorldRotation.Forward, direction );
            if ( angle > ViewAngle / 2f ) continue;

            // Проверяем прямую видимость (нет стен между)
            var tr = Scene.Trace
                .Ray( npc.WorldPosition + Vector3.Up * 64, player.WorldPosition + Vector3.Up * 64 )
                .IgnoreGameObjectHierarchy( npc.GameObject )
                .Run();

            if ( tr.GameObject == player.GameObject || 
                 tr.GameObject?.GetComponentInParent<Player>() == player )
            {
                // NPC видит игрока!
                npc.Target = player.GameObject;
                return;
            }
        }
    }
}
```

### Как работает зрение:

```
           ViewAngle = 120°
              ╱─────╲
             ╱       ╲
            ╱    ●    ╲          ● = игрок (виден!)
           ╱  NPC →    ╲
            ╲         ╱
             ╲       ╱
              ╲─────╱
                              ○ = игрок за стеной (не виден!)
```

## ✅ Проверка

1. NPC появляется на карте и стоит
2. Если видит игрока — начинает преследовать
3. Получает урон и создаёт ragdoll при смерти
4. Ходит по NavMesh, обходит препятствия

## Что мы создали:

- [x] `Npc.cs` — базовый NPC (навигация, здоровье, расписания)
- [x] `ScheduleBase.cs` — базовое расписание
- [x] `TaskBase.cs` — базовая задача
- [x] `MoveTo.cs`, `Wait.cs` — конкретные задачи
- [x] `SensesLayer.cs` — зрение NPC

---

**Далее**: [Этап 16: Сохранение и загрузка →](16_Сохранение.md)
