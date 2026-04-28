# 28.04 — CitizenAnimationHelper

## Что мы делаем?

Знакомимся с **`CitizenAnimationHelper`** — стандартным помощником для Citizen-скелета. Это обёртка над `SkinnedModelRenderer.Set("...")`, которая превращает «магические строки» в нормальные C#-свойства. Если делаешь персонажа на базе Citizen — пиши через `CitizenAnimationHelper`, а не напрямую в граф.

## Зачем нужен helper

Без него каждый раз приходится писать так:

```csharp
renderer.Set( "move_x", forward );
renderer.Set( "move_y", sideward );
renderer.Set( "move_z", velocity.z );
renderer.Set( "move_speed", velocity.Length );
renderer.Set( "aim_pitch", camera.Angles().pitch );
renderer.Set( "aim_yaw",   camera.Angles().yaw   );
renderer.Set( "holdtype", (int)HoldTypes.Rifle );
```

С хелпером:

```csharp
var anim = new CitizenAnimationHelper( renderer );
anim.WithVelocity( controller.Velocity );
anim.WithLook( camera.Forward );
anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
```

Один объект — и десяток параметров заполнен правильно, с учётом конвертации в локальные оси, ремапа скоростей и т.п.

## Где живёт

`CitizenAnimationHelper` — часть **`Sandbox.Citizen`** (стандартная библиотека s&box). Подключается через `using Sandbox.Citizen;` — так это сделано в `Code/Npcs/Layers/AnimationLayer.Hold.cs` (см. 28.05).

В свежем коде Sandbox практическую работу с Citizen-параметрами выполняет встроенный **`PlayerController`** (компонент движка) — он сам каждый кадр вызывает аналог `WithVelocity` для своего `SkinnedModelRenderer`. Поэтому в `Player.cs` Sandbox’а тебе не нужно вручную ставить `move_x` — это уже сделано. Но есть две вещи, которые проект **должен** ставить сам:

1. **`holdtype`** — что игрок держит в руках (см. ниже).
2. **`b_noclip`, `duck`** и подобное — если у тебя свой режим движения (см. `NoclipMoveMode.cs`).

## Самые ходовые свойства / методы

| Свойство / метод | Что делает | Кладёт в граф |
|---|---|---|
| `WithVelocity( Vector3 v )` | Заполняет всё движение из мировой скорости. | `move_x`, `move_y`, `move_z`, `move_speed`, `move_groundspeed`, `move_direction` |
| `WithLook( Vector3 forward, float eyesWeight = 1, float headWeight = 1, float bodyWeight = 1 )` | Куда смотрит камера/глаза/голова/корпус. | `aim_pitch`, `aim_yaw`, `look_dir`, веса |
| `WithWishVelocity( Vector3 v )` | «Куда хочет идти», даже если стоит (для разворотов). | внутренние параметры |
| `HoldType` | Что в руках. | `holdtype` |
| `Handedness` | Право/лево/двумя руками. | `holdtype_handedness` |
| `MoveStyle` | Auto/Walk/Run — стиль ходьбы. | внутренний |
| `IkLeftHand` / `IkRightHand` | `Transform?` — мировая цель для руки. | IK-чейн `hand_left/right` |
| `IkLeftFoot` / `IkRightFoot` | То же для ног (адаптация к рельефу). | IK-чейн `foot_left/right` |
| `DuckLevel` | 0..1 присест. | `duck` |
| `IsGrounded` | На земле. | `b_grounded` |
| `IsNoclipping` | В noclip. | `b_noclip` |
| `TriggerJump()` | Один раз сыграть прыжок. | триггер `b_jump` |
| `TriggerDeploy()` | Один раз сыграть deploy. | триггер |

## Перечисления

```csharp
public enum HoldTypes
{
    None,
    Pistol,
    Rifle,
    Shotgun,
    HoldItem,
    Punch,
    Swing,
    RPG,
}

public enum Hand
{
    Both,
    Right,
    Left,
}

public enum MoveStyles
{
    Auto,
    Walk,
    Run,
}
```

## Где это в Sandbox

### 1. Holdtype — переключение анимации под оружие

`Code/Player/PlayerInventory.cs:479`:

```csharp
renderer.Set( "holdtype", (int)ActiveWeapon.HoldType );
...
renderer.Set( "holdtype", (int)CitizenAnimationHelper.HoldTypes.None );
```

Каждое оружие (`BaseWeapon.HoldType`) возвращает один из `HoldTypes`. Когда игрок переключает слот — инвентарь пишет правильный `holdtype` в граф, и руки/корпус мгновенно «переучиваются» под пистолет/винтовку/гранату.

### 2. Hold-поза для произвольной пропы (NPC)

`Code/Npcs/Layers/AnimationLayer.Hold.cs:111`:

```csharp
_renderer?.Set( "holdtype",            (int)CitizenAnimationHelper.HoldTypes.HoldItem );
_renderer?.Set( "holdtype_pose",       _holdPose );
_renderer?.Set( "holdtype_handedness", (int)(_oneHanded ? Hand.Right : Hand.Left) );
```

NPC выбрал, как тащить пропу: `holdtype = HoldItem`, поза от 0 до 5 (см. 28.05), и одна или две руки.

### 3. Noclip

`Code/Player/NoclipMoveMode.cs:21`:

```csharp
renderer.Set( "b_noclip", true );
renderer.Set( "duck",     0f   );
```

В noclip Citizen встаёт в «летательную» позу, выпрямляется (без присеста).

## Свой персонаж — пример

Если делаешь NPC на Citizen-скелете в своём режиме, минимум каждого кадра:

```csharp
protected override void OnUpdate()
{
    var anim = new CitizenAnimationHelper( Renderer );
    anim.WithVelocity( CharacterController.Velocity );
    anim.WithLook( EyeRotation.Forward );
    anim.HoldType = CurrentWeapon?.HoldType ?? HoldTypes.None;
    anim.IsGrounded = CharacterController.IsOnGround;
    anim.DuckLevel = IsDucking ? 1f : 0f;
}
```

Этого достаточно, чтобы Citizen «жил».

## Подсказки на практике

- **Не используй `CitizenAnimationHelper` на не-Citizen-скелете.** Имена параметров не совпадут — будет тишина.
- **Helper — `struct`** (лёгкий объект): создавай каждый кадр без боязни аллокаций.
- **Если лень разбираться** в `WithVelocity` — глянь `Sandbox.Citizen`-исходники или просто смотри, что встроенный `PlayerController` уже делает за тебя.
- **HoldType пиши только при смене оружия**, а не каждый кадр (хотя и не сломается).

## Результат

После этого этапа ты знаешь:

- ✅ Что `CitizenAnimationHelper` — обёртка над параметрами графа Citizen.
- ✅ Главные свойства: `WithVelocity`, `WithLook`, `HoldType`, `Handedness`, `IkLeft/Right`, `DuckLevel`, `IsGrounded`.
- ✅ Что в Sandbox из этого активно используется (`HoldType`, `b_noclip`, `holdtype_pose` для NPC).
- ✅ Как написать свой `OnUpdate` для NPC на Citizen.

---

📚 **Официальная документация Facepunch:** [animation/citizen-animation-helper.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/animation)

**Следующий шаг:** [28.05 — Разбор AnimationLayer.Hold.cs](28_05_AnimationLayer_Hold.md)
