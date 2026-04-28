# 28.12 — Material System: .vmat и параметры из C#

## Что мы делаем?

Изучаем **материалы** (`.vmat`) — обёртку над шейдером с конкретными значениями параметров. Параллельно учимся **переопределять** параметры на лету из C# через `SceneObject.Attributes.Set(...)` и `Material.Set(...)`. Без этого никакой динамический эффект не сделаешь — материал в файле статичен.

## Что такое .vmat

`.vmat` — это текстовый файл, который в Asset Browser отображается как ассет «Material». Содержание примерно такое:

```vmat
Layer0
{
    shader  "shaders/vr_complex.shader"

    F_TRANSPARENCY  0
    F_NORMAL_MAP    1

    g_vColorTint        "[1.0 1.0 1.0 1.0]"
    g_flMetalness       "0.500"
    g_flRoughness       "0.300"

    TextureColor        "materials/wood/oak_color.tga"
    TextureNormal       "materials/wood/oak_normal.tga"
    TextureRoughness    "materials/wood/oak_rough.tga"
}
```

Структура:

| Секция | Что |
|---|---|
| `shader` | Какой `.shader` использовать. |
| `F_*` | Значения **features** (compile-time). |
| `g_*`, `Texture*` | Значения **runtime-параметров** шейдера, объявленных через `Attribute(...)`. |

В редакторе материалов это выглядит как форма: ползунки для float’ов, color picker для Color, поле выбора текстуры.

## Создание материала

В Sandbox `Assets/materials/` уже забит готовыми материалами для оружия, эффектов и пропов. Если тебе нужен новый:

1. Asset Browser → правый клик → **Create → Material**.
2. В открывшемся редакторе выбери шейдер (обычно `vr_complex` для PBR).
3. Натравь текстуры/числа.
4. Сохрани (`Ctrl+S`) — рядом появится `.vmat`.

`.vmat`-ы бывают **самостоятельные** (один на ассет) и **shared** (один на много моделей). Sandbox’ный паттерн: shared в `Assets/materials/game/`, специфичные — рядом с моделью.

## Привязка материала к модели

В `.vmdl`-инспекторе видно список **материал-слотов** меша. Каждому слоту можно либо оставить дефолтный (приходит из FBX), либо подменить на свой `.vmat`. Тогда модель будет рисоваться этим материалом.

Один материал можно использовать на N моделях — все они потянут одни параметры.

## Override материала из C#

Иногда нужно дать одному инстансу свой материал (а не всем):

```csharp
// дать рендереру другой материал в слот 0
modelRenderer.MaterialOverride = Material.Load( "materials/special.vmat" );

// или для конкретного слота
modelRenderer.SetMaterialOverride( index: 1, material: ... );
```

Это **не меняет ассет на диске**, только данный инстанс.

## Параметры на лету: SceneObject.Attributes

Главный механизм динамики: **Attributes** — пара `name -> value`, которая прокидывается прямо в шейдер на каждом кадре.

```csharp
// у компонента ModelRenderer / SkinnedModelRenderer
modelRenderer.SceneObject.Attributes.Set( "Tint",       new Color( 1, 0, 0, 1 ) );
modelRenderer.SceneObject.Attributes.Set( "Roughness",  0.7f );
modelRenderer.SceneObject.Attributes.Set( "AimPoint",   hitPoint );
```

Имя `"Tint"` должно совпадать с `Attribute("Tint")` в `PS` шейдера. Тип — соответствовать (`float`, `Color/float4`, `Vector3/float3`, `Vector2/float2`, `int`).

`Attributes.Set` **переопределяет** то, что в `.vmat`. Если убрать из коллекции — вернётся значение из `.vmat`.

### Пример 1 — мигающее свечение пропы

```csharp
public sealed class BlinkEmissive : Component
{
    [RequireComponent] public ModelRenderer Renderer { get; set; }
    [Property] public Color BaseColor { get; set; } = Color.Red;

    protected override void OnUpdate()
    {
        var k = (MathF.Sin( Time.Now * 4f ) * 0.5f + 0.5f);
        Renderer.SceneObject.Attributes.Set( "EmissionTint", BaseColor * k );
    }
}
```

Шейдер должен иметь `Attribute("EmissionTint")` — иначе ничего не произойдёт (без ошибки).

### Пример 2 — `snap_grid.shader` в Sandbox

В `Assets/shaders/snap_grid.shader` объявлены параметры:

```hlsl
float3 GridOrigin   < Attribute( "GridOrigin" );  Default3( 0, 0, 0 ); >;
float3 AimPoint     < Attribute( "AimPoint" );    Default3( 0, 0, 0 ); >;
float  CellSize     < Attribute( "CellSize" );    Default( 16.0 ); >;
float4 GridColor    < Attribute( "GridColor" );   Default4( 1, 1, 1, 0.5 ); >;
// и т.д.
```

В коде Sandbox `SnapGrid` каждый кадр пишет в материал актуальные значения:

```csharp
material.Set( "GridOrigin", currentOrigin );
material.Set( "AimPoint",   aimWorldPos   );
material.Set( "CellSize",   16f           );
```

— и сетка следует за курсором. См. подробный разбор в [28.13](28_13_SnapGrid_Shader.md).

### Пример 3 — пост-эффект (sniper_scope)

`Code/Weapons/Sniper/SniperScopeEffect.cs:15`:

```csharp
Attributes.Set( "BlurAmount", blurAmount );
Attributes.Set( "Offset",     Vector2.Zero );

_cachedMaterial ??= Material.FromShader( "shaders/postprocess/sniper_scope.shader" );
var blit = BlitMode.WithBackbuffer( _cachedMaterial, Stage.AfterPostProcess, 200, false );
Blit( blit, "SniperScope" );
```

`Attributes` тут — поле базового класса `BasePostProcess`. Подробнее в [28.14](28_14_PostProcess_Shaders.md) / [28.15](28_15_Custom_PostProcess.md).

## Material API: коротко

| Метод / свойство | Что делает |
|---|---|
| `Material.Load(path)` | Загрузить `.vmat` по пути. |
| `Material.FromShader(path)` | Сделать «голый» материал из `.shader`, без файла. |
| `material.Set( "Name", value )` | Переопределить параметр прямо на материале. |
| `material.Attributes` | Коллекция overrides. |
| `material.Shader` | Доступ к шейдеру. |
| `Texture.Load("path/to.tga")` | Загрузить текстуру; передаётся в `material.Set("Tex", tex)`. |

## Когда что использовать

- **`.vmat` правится в редакторе** — для статичных вещей (цвет дерева, шероховатость металла).
- **`SceneObject.Attributes.Set` каждый кадр** — для динамики на конкретном инстансе (миганье, прицельная точка, бойлер для одной пропы).
- **`material.Set`** напрямую — для динамики, общей всем инстансам этого материала (например, общий effect-таймер). Аккуратно: меняет везде.
- **`MaterialOverride`** — когда нужно полностью подменить материал у инстанса.

## Типы параметров → HLSL → C#

| HLSL | C# |
|---|---|
| `float`  | `float` |
| `float2` | `Vector2` |
| `float3` | `Vector3` или `Color` (RGB) |
| `float4` | `Color` (RGBA) или `Vector4` |
| `int`    | `int` |
| `Texture2D` | `Texture` |

## Подсказки на практике

- **Опечатка в имени = тишина.** Никаких ошибок, просто параметр игнорится. Дублируй имена через `const string`.
- **Текстуры в `Attributes` идут реже** — обычно через `material.Set("TextureName", texture)`.
- **`Attributes` лучше, чем `material.Set`** в большинстве случаев: не «портит» общий материал.
- **Если `Attributes.Set` не действует** — проверь, что у `SceneObject` действительно тот шейдер. Если назначен `vr_complex`, твой `Attribute("Tint")` из своего шейдера ничего не сделает.

## Результат

После этого этапа ты знаешь:

- ✅ Что такое `.vmat` и как он устроен внутри.
- ✅ Как создать материал и привязать к модели.
- ✅ Три способа управлять параметрами: `.vmat`-файл, `SceneObject.Attributes.Set`, `material.Set`.
- ✅ Как соответствуют типы HLSL и C#.
- ✅ Видел реальные примеры из Sandbox: `snap_grid`, `sniper_scope`.

---

📚 **Официальная документация Facepunch:** [shaders/material.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/shaders)

**Следующий шаг:** [28.13 — Разбор snap_grid.shader](28_13_SnapGrid_Shader.md)
