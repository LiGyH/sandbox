# 28.03 — Параметры AnimGraph и API SkinnedModelRenderer

## Что мы делаем?

Изучаем главный мост между **C#-кодом** и **AnimGraph** — параметры графа. Это ровно те «магические строки» вида `renderer.Set("holdtype", 1)`, которые встречаются по всему `Code/`. После этого этапа ты сможешь читать чужой код игрока и сразу понимать, что он говорит графу.

## Где параметры объявлены

В редакторе графа есть панель **Parameters** — там добавляются «слоты», у каждого:

- **Имя** (строка, регистр важен): `holdtype`, `move_x`, `b_attack`.
- **Тип**: `bool`, `int`, `float`, `Vector3`, `Rotation`.
- **Default** — значение по умолчанию.
- **Сетевое поведение** — синхронизировать ли параметр с клиентами автоматически.

Конвенция имён в Citizen-графе:

| Префикс | Тип | Пример |
|---|---|---|
| `b_` | bool (булевы триггеры/флаги) | `b_attack`, `b_reload`, `b_grounded`, `b_noclip` |
| `move_` | float/Vector3 (движение) | `move_x`, `move_y`, `move_z`, `move_speed`, `move_groundspeed` |
| `aim_` | float (прицел) | `aim_pitch`, `aim_yaw`, `aim_pitch_inertia` |
| без префикса | int / enum | `holdtype`, `duck`, `deploy_type` |

Это именно конвенция, движок её не проверяет — но придерживайся, чтобы код читался.

## API SkinnedModelRenderer — Set / Get

`SkinnedModelRenderer.Set(...)` — записать параметр в граф. Перегружен по типу:

```csharp
SkinnedModelRenderer renderer = ...;

renderer.Set( "b_attack", true );          // bool   — триггер
renderer.Set( "holdtype", 2 );             // int    — selector
renderer.Set( "move_x",   forward );       // float  — BlendSpace
renderer.Set( "look_dir", lookVector );    // Vector3
renderer.Set( "head_rot", rotation );      // Rotation
```

Чтение — `Get*`:

```csharp
float  speed  = renderer.GetFloat( "move_speed" );
int    hold   = renderer.GetInt(   "holdtype"  );
bool   atk    = renderer.GetBool(  "b_attack"  );
Vector3 v     = renderer.GetVector("look_dir"  );
```

## bool — триггер vs флаг

`b_attack` и `b_grounded` ведут себя по-разному, и это **настраивается в графе**, не в коде:

- **Флаг (sticky bool)** — «пока true, играй пере-/пост-стейт». Пример: `b_grounded` — пока true, играем приземлённую анимацию.
- **Триггер (one-shot)** — «при выставлении в true один раз проиграй переход и сам сбрось обратно в false». Пример: `b_attack` — нажал стрельбу → `Set("b_attack", true)` → граф сам сбросит на false после проигрыша.

В коде ты часто видишь именно установку в `true` и **никогда** обратно в `false`:

```csharp
// ViewModel.cs:152
Renderer?.Set( "b_attack", true );
```

— это триггер. Граф сам сбросит. А вот `b_reload`:

```csharp
Renderer?.Set( "b_reload", true  );  // OnReloadStart
...
Renderer?.Set( "b_reload", false );  // OnReloadFinish
```

— это **флаг**: пока `true`, играем луп перезарядки; станет `false` — выйдем по transition.

## Типичные параметры Citizen

| Параметр | Тип | Зачем |
|---|---|---|
| `move_x`, `move_y` | float | Скорость по локальным осям (вперёд/в сторону) — для BlendSpace 2D. |
| `move_z` | float | Вертикальная скорость — для прыжка/падения. |
| `move_speed` | float | Модуль скорости. |
| `move_groundspeed` | float | Скорость, спроецированная на пол. |
| `move_direction` | float | Угол движения относительно взгляда. |
| `move_bob` | float | Сила «болтанки» камеры/оружия. |
| `aim_pitch`, `aim_yaw` | float | Куда смотрит камера, для additive-прицеливания. |
| `holdtype` | int | Что в руках: None/Pistol/Rifle/Shotgun/HoldItem/… |
| `holdtype_pose` | float | Поза удержания пропа (0–5, см. 28.05). |
| `holdtype_handedness` | int | Право/лево/обе руки. |
| `duck` | float | Степень присеста (0..1). |
| `b_grounded`, `b_noclip` | bool (флаги) | На земле / в noclip. |
| `b_attack`, `b_reload`, `b_pull`, `b_throw` | bool (триггеры) | Эпизодические действия. |

## Теги анимаций (anim tags)

Помимо параметров **графа**, у клипов есть **теги** — отметки на таймлайне внутри `.vanim`. Они используются:

1. **В графе** — как условие перехода: «дождись тега `can_exit` перед сменой стейта».
2. **В C#** — как событие: «когда клип дойдёт до тега `footstep_left` — сыграть звук» (см. 28.08).

Тег ставится в редакторе модели прямо на временной шкале. В C# ловится через события `SkinnedModelRenderer.OnGenericEvent` / `OnFootstepEvent` (зависит от типа тега).

## Кости и доступ к скелету

`SkinnedModelRenderer` даёт доступ к костям как к `GameObject`:

```csharp
// Player.cs:113
var boneObject = playerRenderer.GetBoneObject( boneName );
```

`boneObject.WorldPosition` / `WorldRotation` — реальная мировая позиция кости после анимации и IK. Это то, как Sandbox присоединяет оружие к руке: создаёт пустой `GameObject`, парентит к `boneObject`, и оружие следует за костью кадр-в-кадр.

Пример из NPC-кода (28.05):

```csharp
var handBone = _renderer?.GetBoneObject( "hold_R" );  // правая «hold-точка»
prop.SetParent( handBone, true );                     // пропа едет с рукой
```

## Подсказки на практике

- **Никогда не угадывай имя параметра.** Открой `.vanmgrph` в редакторе и прочитай. Иначе `Set("Holdtype", ...)` (с большой H) молча ничего не сделает.
- **Бери `CitizenAnimationHelper`,** если работаешь с Citizen-скелетом — он скрывает имена параметров за свойствами. См. 28.04.
- **Для триггеров не пиши `Set(..., false)`** — это часто не нужно, граф сам сбрасывает.
- **Пиши параметры каждый кадр** в `OnUpdate`, если они зависят от скорости/угла — иначе анимация замрёт на последнем значении.

## Результат

После этого этапа ты знаешь:

- ✅ Какие типы параметров бывают (`bool/int/float/Vector3/Rotation`).
- ✅ Разницу между **флагом** и **триггером** в `bool`-параметрах.
- ✅ Как читать/писать параметры через `SkinnedModelRenderer.Set / GetFloat / GetInt / ...`.
- ✅ Какие имена принято использовать в Citizen-графе.
- ✅ Что такое теги анимаций и зачем они нужны.

---

📚 **Официальная документация Facepunch:** [animation/animgraph-parameters.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/animation)

**Следующий шаг:** [28.04 — CitizenAnimationHelper](28_04_CitizenAnimationHelper.md)
