# Этап 9: Physics Gun — Физическая пушка

## Цель этого этапа

Создать Physics Gun (физическую пушку) — оружие, которое позволяет хватать, двигать, вращать и замораживать объекты. Это одно из **главных** инструментов Sandbox.

## Как работает Physics Gun

```
1. Зажал ЛКМ → луч из глаз → попал в объект → СХВАТИЛ
2. Двигаешь мышь → объект следует за курсором
3. Колёсико → приближение/удаление объекта
4. Shift + мышь → вращение объекта
5. Отпустил ЛКМ → ОТПУСТИЛ объект
6. ПКМ → ЗАМОРОЗИЛ объект (он висит в воздухе)
```

## Шаг 9.1: Основной класс Physgun

Создай файл `Code/Weapons/PhysGun/Physgun.cs`:

```csharp
/// <summary>
/// Physics Gun — хватает и двигает объекты.
/// Наследует BaseCarryable напрямую (не BaseWeapon — нет патронов).
/// </summary>
public partial class Physgun : BaseCarryable
{
    // --- Настройки ---
    
    [Property] public float MinGrabDistance { get; set; } = 64f;
    [Property] public float MaxGrabDistance { get; set; } = 4096f;
    [Property] public float GrabStrength { get; set; } = 20f;

    // --- Состояние ---
    
    /// <summary>
    /// Объект, который сейчас схвачен.
    /// </summary>
    [Sync] public Rigidbody GrabbedBody { get; private set; }
    
    /// <summary>
    /// Расстояние до схваченного объекта.
    /// </summary>
    [Sync] public float GrabDistance { get; set; }
    
    /// <summary>
    /// Вращаем ли объект сейчас (Shift зажат).
    /// </summary>
    [Sync] public bool IsRotating { get; set; }

    /// <summary>
    /// Текущий целевой поворот схваченного объекта.
    /// </summary>
    [Sync] public Rotation GrabRotation { get; set; }

    // --- Ввод ---

    public override void OnPlayerUpdate( Player player )
    {
        if ( Input.Pressed( "attack1" ) )
        {
            TryGrab();
        }

        if ( !Input.Down( "attack1" ) && GrabbedBody.IsValid() )
        {
            Release();
        }

        if ( Input.Pressed( "attack2" ) )
        {
            Freeze();
        }

        if ( GrabbedBody.IsValid() )
        {
            UpdateGrab();
        }
    }

    // --- Захват ---

    /// <summary>
    /// Попытка схватить объект.
    /// </summary>
    [Rpc.Host]
    private void TryGrab()
    {
        var player = Owner;
        if ( !player.IsValid() ) return;

        var eyePos = player.EyeTransform.Position;
        var eyeDir = player.EyeTransform.Forward;

        // Пускаем луч
        var tr = Scene.Trace
            .Ray( eyePos, eyePos + eyeDir * MaxGrabDistance )
            .IgnoreGameObjectHierarchy( player.GameObject )
            .WithoutTags( "player", "trigger" )
            .Run();

        if ( !tr.Hit ) return;

        // Ищем Rigidbody на объекте
        var body = tr.GameObject?.GetComponent<Rigidbody>();
        if ( !body.IsValid() ) return;

        // Нельзя хватать статические объекты (стены карты)
        if ( body.PhysicsBody?.BodyType == PhysicsBodyType.Static )
            return;

        // Схватили!
        GrabbedBody = body;
        GrabDistance = (tr.HitPosition - eyePos).Length;
        GrabRotation = body.WorldRotation;

        // Выключаем гравитацию для схваченного объекта
        body.Gravity = false;
    }

    // --- Обновление позиции ---

    /// <summary>
    /// Каждый кадр обновляем позицию схваченного объекта.
    /// </summary>
    private void UpdateGrab()
    {
        if ( !GrabbedBody.IsValid() )
        {
            Release();
            return;
        }

        var player = Owner;
        if ( !player.IsValid() ) return;

        // Колёсико мыши — приближение/удаление
        var scroll = Input.MouseWheel.y;
        if ( scroll != 0 )
        {
            GrabDistance = Math.Clamp( 
                GrabDistance + scroll * 20f, 
                MinGrabDistance, MaxGrabDistance );
        }

        // Вращение объекта (зажат Shift)
        IsRotating = Input.Down( "run" );
        if ( IsRotating )
        {
            var mouseDelta = Input.MouseDelta;
            GrabRotation *= Rotation.FromAxis( Vector3.Up, mouseDelta.x * 0.5f );
            GrabRotation *= Rotation.FromAxis( Vector3.Right, mouseDelta.y * 0.5f );
        }

        // Целевая позиция = перед глазами на расстоянии GrabDistance
        var targetPos = player.EyeTransform.Position 
            + player.EyeTransform.Forward * GrabDistance;

        // Плавно двигаем объект к цели
        var direction = targetPos - GrabbedBody.WorldPosition;
        GrabbedBody.Velocity = direction * GrabStrength;

        // Плавно поворачиваем
        var angleDiff = GrabbedBody.WorldRotation.Angles() - GrabRotation.Angles();
        GrabbedBody.AngularVelocity = -angleDiff * 0.5f;
    }

    // --- Отпускание ---

    /// <summary>
    /// Отпустить объект.
    /// </summary>
    [Rpc.Host]
    private void Release()
    {
        if ( !GrabbedBody.IsValid() ) return;

        // Включаем гравитацию обратно
        GrabbedBody.Gravity = true;
        GrabbedBody = null;
    }

    // --- Заморозка ---

    /// <summary>
    /// Заморозить объект в пространстве (ПКМ).
    /// </summary>
    [Rpc.Host]
    private void Freeze()
    {
        var player = Owner;
        if ( !player.IsValid() ) return;

        // Если держим объект — замораживаем его
        if ( GrabbedBody.IsValid() )
        {
            FreezeBody( GrabbedBody );
            Release();
            return;
        }

        // Если не держим — стреляем лучом и замораживаем
        var eyePos = player.EyeTransform.Position;
        var eyeDir = player.EyeTransform.Forward;

        var tr = Scene.Trace
            .Ray( eyePos, eyePos + eyeDir * MaxGrabDistance )
            .IgnoreGameObjectHierarchy( player.GameObject )
            .Run();

        if ( !tr.Hit ) return;

        var body = tr.GameObject?.GetComponent<Rigidbody>();
        if ( !body.IsValid() ) return;

        // Переключаем заморозку
        if ( body.PhysicsBody?.BodyType == PhysicsBodyType.Kinematic )
        {
            // Размораживаем
            body.PhysicsBody.BodyType = PhysicsBodyType.Dynamic;
            body.Gravity = true;
        }
        else
        {
            FreezeBody( body );
        }
    }

    private void FreezeBody( Rigidbody body )
    {
        body.Velocity = Vector3.Zero;
        body.AngularVelocity = Vector3.Zero;
        body.PhysicsBody.BodyType = PhysicsBodyType.Kinematic;
        body.Gravity = false;
    }
}
```

### Как работает физика захвата:

```
Каждый кадр:
1. Считаем targetPos = глаза + направление × расстояние
2. Считаем direction = targetPos - текущая позиция объекта
3. Velocity = direction × GrabStrength

Объект "притягивается" к целевой точке:

    Глаз ────────────── targetPos
                             ↑
                    direction │
                             │
                        ● объект
                        (Velocity = direction × 20)
```

Чем дальше объект от цели — тем быстрее он к ней летит. Множитель `GrabStrength` определяет «жёсткость» захвата.

### Заморозка (BodyType):

- **Dynamic** — обычное физическое тело (падает, сталкивается)
- **Kinematic** — тело «заморожено» в пространстве. Не реагирует на силы, не падает
- **Static** — неподвижная стена карты. Нельзя двигать вообще

## ✅ Проверка

1. **ЛКМ** — зажми на объекте → объект схвачен и следует за курсором
2. **Колёсико** — приближает/удаляет объект
3. **Shift + мышь** — вращает объект
4. **Отпусти ЛКМ** — объект падает
5. **ПКМ** — объект замерзает в воздухе

## Что мы создали:

- [x] `Physgun.cs` — полная физическая пушка
- [x] Захват, перемещение, вращение, заморозка
- [x] Понимание: Velocity, AngularVelocity, BodyType

---

**Далее**: [Этап 10: Tool Gun — основа →](10_ToolGun_основа.md)
