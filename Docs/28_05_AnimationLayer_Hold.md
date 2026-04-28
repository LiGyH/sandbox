# 28.05 — Разбор AnimationLayer.Hold.cs (NPC берёт и держит пропу)

## Что мы делаем?

Разбираем по шагам реальный файл из проекта — `Code/Npcs/Layers/AnimationLayer.Hold.cs`. Это пример того, как **код управляет анимацией** в Sandbox, когда NPC берёт в руки произвольный объект (любая пропа, не оружие). Здесь сходятся почти все темы Фазы 28: `holdtype`, `holdtype_pose`, IK, кости, парентинг к скелету.

## Файл целиком

См. [Code/Npcs/Layers/AnimationLayer.Hold.cs](../Code/Npcs/Layers/AnimationLayer.Hold.cs). Здесь пройдём по логическим блокам.

## Контекст

`AnimationLayer` — слой «анимационного поведения» NPC. Этот partial-файл отвечает за **держание пропы**. Поля класса:

```csharp
public GameObject HeldProp => _heldProp;

private GameObject _heldProp;   // что держим
private float _holdPose;        // позиция в шкале 0..5
private bool  _oneHanded;       // одной рукой или двумя
// _renderer (из соседнего файла) — SkinnedModelRenderer NPC
// Npc       — сам объект NPC
```

## SetHeldProp — взять пропу

### Шаг 1. Подготовка

```csharp
if ( !prop.IsValid() ) return;
_heldProp = prop;

var rb = prop.GetComponent<Rigidbody>( true );
var mass = rb?.Mass ?? 1f;
if ( rb.IsValid() ) rb.Enabled = false;
```

Запоминаем пропу, **выключаем физику** (иначе она будет драться с парентингом к руке) и сохраняем массу — она пригодится для выбора позы.

### Шаг 2. Замеряем размер

```csharp
var bounds   = prop.GetBounds();
var size     = bounds.Size;
var width    = MathF.Max( size.x, size.y );
var diagonal = size.Length;
```

Граф Citizen умеет «держать предмет» в позе с параметром `holdtype_pose` от 0 до 5. Шкала такая:

| `holdtype_pose` | Что значит | Когда |
|---|---|---|
| 0–2 | близкий хват у груди | тяжёлое и небольшое |
| 2–4 | руки вытянуты вперёд | обычное |
| 4–5 | поднято над головой | большое |

Дальше код **выбирает** позу по габаритам и массе:

```csharp
_oneHanded = diagonal < 32f && mass <= 128;

if ( diagonal >= 64f )
{
    // большое — над головой
    var t = ((diagonal - 64f) / 64f).Clamp( 0f, 1f );
    _holdPose = 4f + t;
    holdOffset = Vector3.Up * 66f + Npc.WorldRotation.Forward * 4f;
}
else if ( mass > 128 )
{
    // тяжёлое — близкий хват
    var t = ((mass - 30f) / 170f).Clamp( 0f, 1f );
    _holdPose = t * 2f;
    holdOffset = Npc.WorldRotation.Forward * 8f + Vector3.Up * 30f;
}
else
{
    // обычное — руки вперёд по ширине
    var t = (width / 32f).Clamp( 0f, 1f );
    _holdPose = 2f + t * 2f;
    var forwardDist = 8 + t * 8f;
    holdOffset = Npc.WorldRotation.Forward * forwardDist + Vector3.Up * 30f;
}
```

`holdOffset` — где **физически** будет висеть пропа относительно NPC (граф меняет позу рук, а сама пропа пристёгивается отдельно).

### Шаг 3. Куда парентить пропу

```csharp
GameObject parent;

if ( _oneHanded )
{
    var handBone = _renderer?.GetBoneObject( "hold_R" );
    parent = handBone ?? Npc.GameObject;
    prop.WorldPosition = parent.WorldPosition;
    prop.WorldRotation = holdRotation;
    prop.SetParent( parent, true );
}
else
{
    var bone = _renderer?.GetBoneObject( "spine_2" );
    parent = bone ?? Npc.GameObject;
    prop.WorldPosition = Npc.WorldPosition + holdOffset;
    prop.WorldRotation = holdRotation;
    prop.SetParent( parent, true );
}
```

Это важный приём:

- **Одной рукой** — парентим прямо к **косточке руки** `hold_R`. Пропа едет вместе с рукой (рука размахивается при ходьбе → пропа размахивается).
- **Двумя руками** — парентим к **спине** `spine_2`. Пропа висит у груди, естественно качается с торсом, а руки IK-ом тянутся к её бокам (см. шаг 5).

`GetBoneObject(name)` — это API `SkinnedModelRenderer`, возвращает «обёртку» кости как `GameObject`. Любой объект, припарентенный к нему, движется вслед за костью кадр-в-кадр после анимации.

### Шаг 4. Сообщаем графу

```csharp
_renderer?.Set( "holdtype",            (int)CitizenAnimationHelper.HoldTypes.HoldItem );
_renderer?.Set( "holdtype_pose",       _holdPose );
_renderer?.Set( "holdtype_pose_hand",  0.005f );
_renderer?.Set( "holdtype_handedness", (int)(_oneHanded ? Hand.Right : Hand.Left) );
```

С этого момента граф рисует Citizen в позе «держит предмет». Без `Set` граф ничего не узнает: пропа была бы прижата к спине, а руки висели бы вдоль туловища.

## ClearHeldProp — отпустить пропу

```csharp
_renderer.Set( "holdtype",            0 );
_renderer.Set( "holdtype_pose",       0f );
_renderer.Set( "holdtype_handedness", 0 );
_renderer.ClearIk( "hand_right" );
_renderer.ClearIk( "hand_left"  );
```

- Обнуляем все hold-параметры — Citizen разводит руки.
- **Снимаем IK** — иначе руки бы продолжали тянуться к мёртвой точке.

Дальше код ставит пропу перед NPC, отвязывает от родителя и включает физику обратно.

## UpdateHeldPropIk — IK каждый кадр

Метод вызывается из `OnUpdate` слоя. Если NPC держит пропу **двумя** руками, мы каждый кадр обновляем мировые цели для рук:

```csharp
var bounds = _heldProp.GetBounds();
var center = bounds.Center;
var halfSpread = MathF.Max( MathF.Max( bounds.Extents.x, bounds.Extents.y ), 12f );

if ( halfSpread > 24 )
{
    // широкая — поддерживаем снизу, ладони вверх
    rightHandPos = center - left * halfSpread + Vector3.Down * bounds.Extents.z;
    leftHandPos  = center + left * halfSpread + Vector3.Down * bounds.Extents.z;
    rightRot = Rotation.LookAt( forward, Vector3.Down );
    leftRot  = Rotation.LookAt( forward, Vector3.Up );
}
else
{
    // узкая — берём с боков, ладони внутрь
    rightHandPos = center - left * halfSpread;
    leftHandPos  = center + left * halfSpread;
    rightRot = Rotation.LookAt( forward, -left );
    leftRot  = Rotation.LookAt( forward, -left );
}

_renderer.SetIk( "hand_right", new Transform( rightHandPos, rightRot ) );
_renderer.SetIk( "hand_left",  new Transform( leftHandPos,  leftRot  ) );
```

`SetIk(chainName, transform)` — даёт IK-чейну графа цель в мире. Citizen-граф сам подкручивает плечо/локоть/кисть так, чтобы кисть оказалась в этой точке. Подробнее про IK — в [28.07](28_07_Procedural_IK.md).

`ClearIk(chainName)` — отключает IK для этой руки, она возвращается к чисто анимационной позе.

## Чему учит этот файл

1. **Анимации = код + граф.** Граф знает «как руки должны выглядеть для `holdtype_pose=3.5`», но **что именно** туда передать решает код в зависимости от мира.
2. **Парентинг к косточке** — простой и мощный способ «прибить» что угодно к скелету. Не нужны ни constraint’ы, ни кастомные узлы графа.
3. **IK не магия.** Это просто `SetIk(name, worldTransform)` каждый кадр. Главное — правильно посчитать целевую точку.
4. **Очистка симметрична.** Поставил `holdtype` — не забудь обнулить. Поставил `SetIk` — не забудь `ClearIk`. Иначе будут вечные «руки в воздухе».

## Подсказки на практике

- **`SkinnedModelRenderer.Set("holdtype", 0)` ≡ `HoldTypes.None`.** Используй `(int)HoldTypes.None` для читаемости.
- **Имена костей** Citizen стандартизованы: `hold_R`, `hold_L`, `spine_2`, `head`, `hand_R`, `hand_L`. Имена IK-чейнов — `hand_right`, `hand_left`, `foot_right`, `foot_left`.
- **Если пропа дёргается** — проверь, что выключил `Rigidbody`. Физика и парентинг конфликтуют.

## Результат

После этого этапа ты:

- ✅ Можешь читать построчно `AnimationLayer.Hold.cs`.
- ✅ Понимаешь шкалу `holdtype_pose` (0–5) и зачем она.
- ✅ Знаешь приём «парентим объект к косточке через `GetBoneObject`».
- ✅ Видел реальное использование `SetIk`/`ClearIk`.

---

📚 **Официальная документация Facepunch:** [animation/citizen-animation-helper.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/animation)

**Следующий шаг:** [28.06 — Анимации view-модели оружия](28_06_ViewModel_Animations.md)
