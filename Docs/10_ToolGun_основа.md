# Этап 10: Tool Gun — основа

## Цель этого этапа

Создать Tool Gun (инструментальную пушку) — оружие, которое переключается между разными **режимами** (tools). Каждый режим — это отдельный инструмент (сварка, верёвка, удалитель и т.д.).

## Архитектура Tool Gun

```
Toolgun (BaseCarryable)
├── Текущий режим (ActiveToolMode)
└── Список всех режимов:
    ├── Weld        ← Сварка
    ├── Rope        ← Верёвка
    ├── Elastic     ← Пружина
    ├── Remover     ← Удалитель
    ├── Resizer     ← Изменение размера
    ├── Balloon     ← Шарик
    ├── Thruster    ← Двигатель
    ├── Wheel       ← Колесо
    └── ... и другие
```

### Ключевой принцип:

Tool Gun **сам** не делает ничего. Он просто **передаёт управление** активному режиму. При нажатии ЛКМ вызывается `ActiveToolMode.OnPrimaryAttack()`.

## Шаг 10.1: Базовый класс ToolMode

Создай файл `Code/Weapons/ToolGun/ToolMode.cs`:

```csharp
/// <summary>
/// Базовый класс для всех режимов Tool Gun.
/// Каждый режим (Weld, Rope, Remover...) наследует этот класс.
/// </summary>
[Icon( "build" )]
public partial class ToolMode : Component
{
    /// <summary>
    /// Название режима для UI.
    /// </summary>
    [Property] public string Title { get; set; }
    
    /// <summary>
    /// Описание для UI.
    /// </summary>
    [Property] public string Description { get; set; }

    /// <summary>
    /// Ссылка на Toolgun, которому принадлежит этот режим.
    /// </summary>
    public Toolgun Toolgun => GetComponentInParent<Toolgun>( true );

    /// <summary>
    /// Игрок, который держит Tool Gun.
    /// </summary>
    public Player Player => Toolgun?.Owner;

    // --- Основные действия ---

    /// <summary>
    /// ЛКМ — основное действие.
    /// </summary>
    public virtual void OnPrimaryAttack() { }

    /// <summary>
    /// ПКМ — вторичное действие.
    /// </summary>
    public virtual void OnSecondaryAttack() { }

    /// <summary>
    /// Перезарядка (R).
    /// </summary>
    public virtual void OnReload() { }

    // --- Трассировка ---

    /// <summary>
    /// Пускает луч из глаз игрока. Используется большинством инструментов.
    /// Возвращает результат попадания.
    /// </summary>
    protected SceneTraceResult DoTrace()
    {
        var player = Player;
        if ( !player.IsValid() ) return default;

        var eyePos = player.EyeTransform.Position;
        var eyeDir = player.EyeTransform.Forward;

        return Scene.Trace
            .Ray( eyePos, eyePos + eyeDir * 4096f )
            .IgnoreGameObjectHierarchy( player.GameObject )
            .WithoutTags( "trigger", "player" )
            .Run();
    }

    // --- HUD ---

    /// <summary>
    /// Рисует информацию на экране (прицел, подсказки).
    /// </summary>
    public virtual void DrawHud( HudPainter hud, Vector2 crosshair ) { }

    // --- Отмена (Undo) ---

    /// <summary>
    /// Добавляет объект в стек отмены.
    /// При нажатии Z объект будет удалён.
    /// </summary>
    protected void AddToUndo( string name, params GameObject[] objects )
    {
        var player = Player;
        if ( !player.IsValid() ) return;

        // Создаём запись отмены
        var undo = player.Undo.Create();
        undo.Name = name;
        foreach ( var go in objects )
        {
            undo.Add( go );
        }
    }
}
```

### Паттерн трассировки:

Почти все инструменты начинают с **луча из глаз игрока**. `DoTrace()` — общий метод для этого:

```
Глаз игрока ──── [луч 4096 единиц] ──→ ...
                        │
                        ↓ попал
                    Объект (prop, стена, NPC...)
```

## Шаг 10.2: Toolgun — сам пистолет

Создай файл `Code/Weapons/ToolGun/Toolgun.cs`:

```csharp
/// <summary>
/// Tool Gun — пистолет инструментов.
/// Содержит список ToolMode и передаёт им управление.
/// </summary>
public partial class Toolgun : BaseCarryable
{
    /// <summary>
    /// Текущий активный режим (Weld, Rope, Remover...).
    /// Синхронизируется по сети.
    /// </summary>
    [Sync( SyncFlags.FromHost )] 
    public ToolMode ActiveToolMode { get; private set; }

    /// <summary>
    /// Все доступные режимы. Они добавлены как компоненты
    /// на дочерних объектах Tool Gun.
    /// </summary>
    public IEnumerable<ToolMode> AllModes => 
        GetComponentsInChildren<ToolMode>( true );

    // --- Переключение режимов ---

    /// <summary>
    /// Установить активный режим по имени типа.
    /// </summary>
    public void SetToolMode( string typeName )
    {
        if ( !Networking.IsHost )
        {
            HostSetToolMode( typeName );
            return;
        }

        var mode = AllModes.FirstOrDefault( 
            x => x.GetType().Name == typeName );
        
        if ( mode.IsValid() )
        {
            ActiveToolMode = mode;
        }
    }

    [Rpc.Host]
    private void HostSetToolMode( string typeName )
    {
        SetToolMode( typeName );
    }

    // --- Передача ввода в активный режим ---

    public override void OnPlayerUpdate( Player player )
    {
        if ( !ActiveToolMode.IsValid() ) return;

        if ( Input.Pressed( "attack1" ) )
        {
            ActiveToolMode.OnPrimaryAttack();
        }

        if ( Input.Pressed( "attack2" ) )
        {
            ActiveToolMode.OnSecondaryAttack();
        }

        if ( Input.Pressed( "reload" ) )
        {
            ActiveToolMode.OnReload();
        }
    }

    // --- HUD ---

    public override void DrawHud( HudPainter painter, Vector2 crosshair )
    {
        ActiveToolMode?.DrawHud( painter, crosshair );

        // Рисуем точку прицела
        painter.DrawCircle( crosshair, 2f, Color.White );
    }
}
```

### Как Tool Gun находит свои режимы:

Все режимы — это **компоненты на дочерних объектах** Tool Gun:

```
GameObject "Toolgun"
├── Toolgun (BaseCarryable)
└── Children:
    ├── "Weld"
    │   └── Weld : ToolMode
    ├── "Rope"
    │   └── Rope : ToolMode
    ├── "Remover"
    │   └── Remover : ToolMode
    └── ... и так далее
```

`GetComponentsInChildren<ToolMode>( true )` находит их все.

## Шаг 10.3: Пример — Remover (Удалитель)

Самый простой инструмент — удалитель. Наведи на объект, нажми ЛКМ — он исчезнет.

Создай файл `Code/Weapons/ToolGun/Modes/Remover.cs`:

```csharp
/// <summary>
/// Удалитель — ЛКМ удаляет объект, на который наведён.
/// </summary>
[Title( "Remover" ), Description( "Remove objects from the world" )]
[Icon( "delete" )]
public class Remover : ToolMode
{
    public override void OnPrimaryAttack()
    {
        if ( !Networking.IsHost ) return;

        var tr = DoTrace();
        if ( !tr.Hit ) return;

        var go = tr.GameObject;
        if ( !go.IsValid() ) return;

        // Нельзя удалять игроков!
        if ( go.Tags.Has( "player" ) ) return;

        // Находим корневой сетевой объект
        var root = go.FindNetworkRoot();
        if ( root.IsValid() )
        {
            root.Destroy();
        }
        else
        {
            go.Destroy();
        }

        // Звук удаления
        RemoveEffects();
    }

    [Rpc.Broadcast]
    private void RemoveEffects()
    {
        Sound.Play( "tool_remove", WorldPosition );
    }
}
```

### Что такое `FindNetworkRoot()`?

Когда ты наводишь на часть сложного объекта (например, на колесо машины), `FindNetworkRoot()` находит **корневой объект** (саму машину). Это наша функция из `EngineAdditions.cs`:

```csharp
// Code/EngineAdditions.cs
extension( GameObject go )
{
    public GameObject FindNetworkRoot()
    {
        if ( go.NetworkMode == NetworkMode.Object ) return go;
        return go.Parent?.FindNetworkRoot();
    }
}
```

Она идёт вверх по иерархии, пока не найдёт объект с `NetworkMode.Object`.

## Шаг 10.4: Пример — Weld (Сварка)

Сварка соединяет два объекта жёстко.

Создай файл `Code/Weapons/ToolGun/Modes/Weld.cs`:

```csharp
/// <summary>
/// Сварка — жёстко соединяет два объекта.
/// Первый клик = выбрать первый объект.
/// Второй клик = выбрать второй и приварить.
/// </summary>
[Title( "Weld" ), Description( "Weld two objects together" )]
[Icon( "link" )]
public class Weld : ToolMode
{
    private GameObject _firstObject;
    private Vector3 _firstHitPoint;

    public override void OnPrimaryAttack()
    {
        if ( !Networking.IsHost ) return;

        var tr = DoTrace();
        if ( !tr.Hit ) return;

        var go = tr.GameObject;
        if ( !go.IsValid() ) return;
        if ( go.Tags.Has( "player" ) ) return;

        // Если первый объект ещё не выбран
        if ( !_firstObject.IsValid() )
        {
            _firstObject = go;
            _firstHitPoint = tr.HitPosition;
            return;
        }

        // Второй объект выбран — свариваем!
        var firstBody = _firstObject.GetComponent<Rigidbody>();
        var secondBody = go.GetComponent<Rigidbody>();

        if ( !firstBody.IsValid() || !secondBody.IsValid() )
        {
            _firstObject = null;
            return;
        }

        // Создаём FixedJoint (жёсткое соединение)
        var joint = _firstObject.AddComponent<FixedJoint>();
        joint.Body = secondBody;
        // Точка соединения — где попал луч
        joint.LocalAnchor = _firstObject.WorldTransform
            .PointToLocal( _firstHitPoint );

        // Очищаем выбор
        _firstObject = null;

        // Эффекты
        WeldEffects();
    }

    /// <summary>
    /// ПКМ — отменить выбор первого объекта.
    /// </summary>
    public override void OnSecondaryAttack()
    {
        _firstObject = null;
    }

    [Rpc.Broadcast]
    private void WeldEffects()
    {
        Sound.Play( "tool_weld", WorldPosition );
    }
}
```

### Как работают суставы (Joints) в физике:

```
Без сварки:              С сваркой (FixedJoint):
                         
  [A] ──── [B]            [A]═══════[B]
   │        │              │          │
   ↓        ↓              ↓          ↓
  Падают   отдельно       Падают КАК ОДИН объект
```

Типы суставов:
- **FixedJoint** — жёсткое (сварка)
- **HingeJoint** — петля (дверь)
- **SpringJoint** — пружина (elastic)
- **SliderJoint** — ползунок

## ✅ Проверка

1. **Tool Gun в руках** — нажимай ЛКМ для активного инструмента
2. **Remover** — удаляет объекты
3. **Weld** — соединяет два объекта
4. **Переключение** — через UI или команду

## Что мы создали:

- [x] `ToolMode.cs` — базовый класс режима
- [x] `Toolgun.cs` — сам пистолет инструментов
- [x] `Remover.cs` — удалитель
- [x] `Weld.cs` — сварка (FixedJoint)

---

**Далее**: [Этап 11: Инструменты — часть 1 →](11_Инструменты_часть1.md)
