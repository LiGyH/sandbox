# 28.11 — Анатомия .shader-файла

## Что мы делаем?

Учимся **читать** `.shader`-файл s&box. После этого этапа у тебя в голове будет «карта» любого шейдера в проекте: где описаны параметры, где вершинный шейдер, где пиксельный, какие include’ы доступны.

## Скелет файла

```hlsl
HEADER       { ... }   // метаданные
MODES        { ... }   // в каких проходах рисуется
FEATURES     { ... }   // статические переключатели (compile-time)
COMMON       { ... }   // общие includes / defines / structs
struct VertexInput { ... };
struct PixelInput  { ... };
VS           { ... }   // вершинный шейдер
PS           { ... }   // пиксельный шейдер
```

Не все блоки обязательны: например, минимальный пост-эффект может не иметь `FEATURES`. Но порядок важен — `COMMON` приходит до `VS`/`PS`, иначе они не увидят includes.

## HEADER

```hlsl
HEADER
{
    Description = "Snap Grid Overlay";
    DevShader   = true;          // показывать только при включённом dev-режиме
}
```

| Ключ | Что делает |
|---|---|
| `Description` | Подпись в Material Editor. |
| `DevShader` | Помечает «не для финального ассета», прячет из общих списков. |
| `Version` | Версия (для миграций). |

## MODES

```hlsl
MODES
{
    Forward();          // основной форвард-проход
    Depth();            // pre-pass глубины (обычно для теней/SSAO)
    ToolsVis( S_MODE_TOOLS_VIS );  // визуализация в редакторе
    VrForward();        // отдельный мод для VR
    Default();          // для пост-эффектов
}
```

`MODES` — это **список проходов**, в которых движок будет вызывать твой шейдер. У `snap_grid.shader` всего один:

```hlsl
MODES
{
    Forward();
}
```

Значит, сетка рисуется только в основном проходе и не пишет глубину/тени.

У `sniper_scope.shader` (пост-эффект):

```hlsl
MODES
{
    Default();
    VrForward();
}
```

`Default()` — стандартный пост-проход; `VrForward()` — отдельный для VR.

## FEATURES

```hlsl
FEATURES
{
    #include "common/features.hlsl"

    Feature( F_TRANSPARENCY,    0..1, "Transparency" );
    Feature( F_SCREEN_SPACE_UV, 0..1, "Use Screen Space UVs" );
}
```

`Feature` — это **compile-time switch**. Под каждое уникальное сочетание включённых feature’ов компилируется отдельная вариация шейдера. Внутри HLSL ты потом проверяешь:

```hlsl
#if ( F_TRANSPARENCY )
    col.a *= alpha;
#endif
```

Это не runtime-if (он стоит дороже), а полное удаление кода в соответствующей вариации.

## COMMON

```hlsl
COMMON
{
    #define CUSTOM_MATERIAL_INPUTS
    #include "common/shared.hlsl"
}
```

Главные includes движка:

| Include | Где использовать |
|---|---|
| `common/shared.hlsl` | Базовый набор для surface/forward шейдеров: матрицы, `Position3WsToPs`, источники света, base material inputs. |
| `common/features.hlsl` | Стандартные `Feature(...)` хелперы. |
| `postprocess/shared.hlsl` | Для пост-эффектов: входная текстура backbuffer, depth-сэмпл, утилиты. |
| `common/utils.hlsl` | Математика: `Remap`, `Saturate`, конверсии цветов. |

`#define CUSTOM_MATERIAL_INPUTS` — отказ от стандартных PBR-инпутов; ты сам объявишь свои `Attribute(...)` в `PS`. Так сделано в `snap_grid.shader`.

## VertexInput / PixelInput

Структуры на границе VS/PS:

```hlsl
struct VertexInput
{
    float3 vPositionWs        : POSITION  < Semantic( PosXyz ); >;
    uint   nInstanceTransformID : TEXCOORD13 < Semantic( InstanceTransformUv ); >;
};

struct PixelInput
{
    float3 vPositionWs : TEXCOORD0;

    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 vPositionPs : SV_Position;
    #endif

    #if ( PROGRAM == VFX_PROGRAM_PS )
        float4 vPositionSs : SV_Position;
    #endif
};
```

Что важно:

1. Каждое поле в `VertexInput` помечено **семантикой** в угловых скобках (`< Semantic( ... ); >`). Это говорит движку, что подкладывать в это поле:
   - `PosXyz` — мировая позиция вершины.
   - `Normal`, `Tangent`, `TexCoord` — нормаль, тангент, UV.
   - `InstanceTransformUv` — индекс инстанс-трансформа (для GPU-инстансинга).
2. `PixelInput` имеет **разный набор полей под VS и PS**:
   - В VS обязательно `SV_Position` — позиция в clip-space (выход).
   - В PS то же поле приходит как `vPositionSs` (screen-space) — тип `SV_Position` ведёт себя по-разному в этих стадиях.

## VS

```hlsl
VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        o.vPositionWs = i.vPositionWs;
        o.vPositionPs = Position3WsToPs( i.vPositionWs );
        return o;
    }
}
```

Минимум, что должен сделать VS — превратить позицию вершины в clip-space через `Position3WsToPs(...)`. Дальше он передаёт в PS всё, что тому нужно (мировые координаты, UV, нормаль и т.п.).

Главные хелперы:

| Хелпер | Что делает |
|---|---|
| `Position3WsToPs(ws)` | Мировая позиция → clip-space (умножение на ViewProjection). |
| `Position3OsToWs(os)` | Object → world (учитывает трансформ инстанса). |
| `Normal3OsToWs(os)`   | Нормаль object → world. |
| `Tangent3OsToWs`      | То же для тангента. |

## PS

`PS` — самое интересное. Здесь:

1. Объявляются **runtime-параметры** через `Attribute(...)`:
   ```hlsl
   float4 GridColor < Attribute( "GridColor" ); Default4( 1.0, 1.0, 1.0, 0.5 ); >;
   ```
   Это и есть «ручка», которую ты потом крутишь из C# через `material.Set("GridColor", new Color(...))` или `Attributes.Set(...)` в `BasePostProcess`.

2. Настраиваются **render states** — режимы блендинга, depth, cull:
   ```hlsl
   RenderState( DepthWriteEnable, false );
   RenderState( DepthEnable,      false );
   RenderState( CullMode,         NONE );
   RenderState( BlendEnable,      true );
   RenderState( SrcBlend,         SRC_ALPHA );
   RenderState( DstBlend,         INV_SRC_ALPHA );
   ```
   Стандартный для UI-overlay набор: рисуем поверх всего, прозрачно, без записи в Z.

3. Главная функция — `MainPs`, возвращает `float4 : SV_Target0`:
   ```hlsl
   float4 MainPs( PixelInput i ) : SV_Target0
   {
       ...
       return col;
   }
   ```

   Можно вернуть несколько `SV_TargetN` (для MRT — multiple render targets) — но в Sandbox это не используется.

4. Можно **дискардить** пиксель:
   ```hlsl
   if ( col.a < 0.002 ) discard;
   ```
   Сильно дешевле, чем смешивать почти-прозрачные пиксели.

## Полный мини-шаблон Forward-шейдера

```hlsl
HEADER  { Description = "My shader"; DevShader = true; }
MODES   { Forward(); }
FEATURES { }
COMMON
{
    #define CUSTOM_MATERIAL_INPUTS
    #include "common/shared.hlsl"
}

struct VertexInput { float3 vPositionWs : POSITION < Semantic( PosXyz ); >; };
struct PixelInput
{
    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 vPositionPs : SV_Position;
    #endif
    #if ( PROGRAM == VFX_PROGRAM_PS )
        float4 vPositionSs : SV_Position;
    #endif
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        o.vPositionPs = Position3WsToPs( i.vPositionWs );
        return o;
    }
}

PS
{
    float4 Tint < Attribute( "Tint" ); Default4( 1, 1, 1, 1 ); >;

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        return Tint;
    }
}
```

Этого достаточно для шейдера, который красит меш сплошным цветом из параметра `Tint`.

## Подсказки на практике

- **`Default*(...)`** обязательны — иначе параметр не покажется в UI материала.
- **Имена `Attribute("...")`** — это ровно те строки, которые ты потом будешь использовать в C# (`Attributes.Set("Tint", ...)`).
- **Имена `Feature` пишут заглавными** с префиксом `F_` — так принято.
- **Не пиши логику в VS**, если можно в PS — VS вызывается «на каждой вершине меша», PS — «на каждом пикселе». Иногда вершин больше.
- **`#if ( PROGRAM == VFX_PROGRAM_PS )`** — стандартная защита: то же поле `SV_Position` нельзя одинаково объявить в VS и PS, выбираешь по PROGRAM.

## Результат

После этого этапа ты знаешь:

- ✅ Структуру `.shader`-файла: `HEADER`, `MODES`, `FEATURES`, `COMMON`, `VS`, `PS`.
- ✅ Что такое семантики (`Semantic(PosXyz)`) и зачем они нужны.
- ✅ Что такое `Attribute(...)` и `Default*(...)` — runtime-параметры.
- ✅ Что такое `RenderState` и какие настройки нужны для overlay/прозрачности.
- ✅ Главные include’ы (`common/shared.hlsl`, `postprocess/shared.hlsl`).

---

📚 **Официальная документация Facepunch:** [shaders/syntax.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/shaders)

**Следующий шаг:** [28.12 — Material System: .vmat и параметры](28_12_Material_System.md)
