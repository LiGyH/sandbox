# 28.13 — Разбор snap_grid.shader (построчно)

## Что мы делаем?

Берём реальный шейдер из Sandbox — `Assets/shaders/snap_grid.shader` — и проходим его **построчно**. Этот шейдер рисует **сетку привязки** (snap grid) для инструмента Stacker / SnapGrid (см. [09.05](09_05_SnapGrid.md)) поверх грани, на которую игрок навёл курсор.

Это идеальный учебный пример: маленький, без освещения, с прозрачностью, с кучей `Attribute(...)` параметров, использует производные `ddx/ddy` для anti-aliasing’а.

## Что в итоге рисуется

При наведении курсора на грань пропы:

- Поверх грани появляется **прозрачная сетка** с шагом `CellSize` (по умолчанию 16 unit’ов).
- Сетка **затухает** по краям квадрата `HalfExtents × HalfExtents`.
- Сетка **затухает** дальше от точки прицеливания (`AimPoint`).
- В точке привязки рисуется **синий крестик** (`CornerColor`) на ближайшем углу/центре.
- Центральные оси и края квадрата подчёркнуты **толстыми линиями** (`CenterLineColor`).

## HEADER + MODES + FEATURES + COMMON

```hlsl
HEADER
{
    Description = "Snap Grid Overlay";
    DevShader = true;
}

MODES
{
    Forward();
}

FEATURES
{
}

COMMON
{
    #define CUSTOM_MATERIAL_INPUTS
    #include "common/shared.hlsl"
}
```

- `DevShader = true` — оверлей служебный, его не должно быть в обычных списках материалов.
- `MODES { Forward(); }` — рисуется в основной форвард-проход. Не пишет глубину, не отдаёт в depth pre-pass.
- `CUSTOM_MATERIAL_INPUTS` — отказываемся от стандартных PBR-инпутов: у нас нет цвета/нормали/шероховатости.

## VertexInput / PixelInput

```hlsl
struct VertexInput
{
    float3 vPositionWs : POSITION < Semantic( PosXyz ); >;
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

VS получает **только мировую позицию** вершины (UV/нормаль не нужны — мы не сэмплим текстуру). PS получит мировую позицию через `vPositionWs` — по ней он сам посчитает положение в 2D-системе сетки.

## VS

```hlsl
VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        o.vPositionWs = i.vPositionWs;
        o.vPositionPs = Position3WsToPs( i.vPositionWs );

        // bit of depth bias didn't hurt nobody
        float flProjDepth = saturate( o.vPositionPs.z / o.vPositionPs.w );
        float flBias      = flProjDepth * 0.0001f;
        o.vPositionPs.z  -= flBias * o.vPositionPs.w;

        return o;
    }
}
```

Что важно:

- Передаём вперёд `vPositionWs` (мировая позиция) — она нужна PS для арифметики.
- `Position3WsToPs` переводит в clip-space.
- **Depth bias** — крошечно сдвигаем глубину на себя, чтобы оверлей не «дрался» с поверхностью пропы (z-fighting). Хитрость: пропорционально `flProjDepth` — то есть вдалеке bias чуть больше, что и нужно.

## PS — render states

```hlsl
RenderState( DepthWriteEnable, false );
RenderState( DepthEnable,      false );
RenderState( CullMode,         NONE );
RenderState( BlendEnable,      true );
RenderState( SrcBlend,         SRC_ALPHA );
RenderState( DstBlend,         INV_SRC_ALPHA );
```

Классический набор overlay:

- `DepthEnable=false` — рисуем поверх всего, не учитывая Z.
- `DepthWriteEnable=false` — не пачкаем Z для следующих объектов.
- `CullMode=NONE` — рисуем обе стороны грани (вдруг пропа повёрнута тыльной стороной).
- `SRC_ALPHA / INV_SRC_ALPHA` — стандартный alpha-blending.

## PS — параметры (Attribute)

```hlsl
float3 GridOrigin    < Attribute( "GridOrigin" ); Default3( 0, 0, 0 ); >;
float3 GridRight     < Attribute( "GridRight" );  Default3( 1, 0, 0 ); >;
float3 GridUp        < Attribute( "GridUp" );     Default3( 0, 0, 1 ); >;

float3 AimPoint      < Attribute( "AimPoint" );   Default3( 0, 0, 0 ); >;
float  MaskRadius    < Attribute( "MaskRadius" ); Default( 48.0 ); >;

float  CellSize      < Attribute( "CellSize" );   Default( 16.0 ); >;

float  SnapCornerX   < Attribute( "SnapCornerX" ); Default( 0 ); >;
float  SnapCornerY   < Attribute( "SnapCornerY" ); Default( 0 ); >;
float  SnapAxisX     < Attribute( "SnapAxisX" );   Default( 1.0 ); >;
float  SnapAxisY     < Attribute( "SnapAxisY" );   Default( 0.0 ); >;

float4 GridColor       < Attribute( "GridColor" );       Default4( 1.0, 1.0, 1.0, 0.5 ); >;
float4 CornerColor     < Attribute( "CornerColor" );     Default4( 0.2, 0.6, 1.0, 1.0 ); >;
float4 CenterLineColor < Attribute( "CenterLineColor" ); Default4( 1.0, 1.0, 1.0, 0.85 ); >;

float2 HalfExtents   < Attribute( "HalfExtents" ); Default2( 48.0, 48.0 ); >;
```

Все эти параметры **C# выставляет каждый кадр**:

- `GridOrigin` — точка-якорь сетки в мире.
- `GridRight`, `GridUp` — два ортогональных вектора, задающих **локальную 2D-систему** на грани.
- `AimPoint` — куда смотрит игрок (для затухания вокруг курсора).
- `MaskRadius` — радиус «зоны интереса» вокруг курсора.
- `CellSize` — шаг сетки (16 unit’ов по умолчанию).
- `SnapCornerX/Y` — целочисленные смещения текущего «угла привязки» в шагах сетки.
- `SnapAxisX/Y` — какие оси крестика подсвечивать.
- `HalfExtents` — половина размера зоны (видимый квадрат сетки).

## PS — главное вычисление

### 1. Из 3D в 2D

```hlsl
float3 p = i.vPositionWs;
float3 offset = p - GridOrigin;
float2 facePos = float2( dot( offset, GridRight ), dot( offset, GridUp ) );
```

Берём пиксель в мире, вычитаем якорь, проецируем на оси `GridRight` / `GridUp`. Получаем **2D-координаты пикселя на грани** в плоскости сетки.

### 2. Сетка с anti-aliasing

```hlsl
float2 cellUv  = facePos / CellSize;
float2 deriv   = max( abs( ddx( cellUv ) ), abs( ddy( cellUv ) ) );
float2 wrapped = abs( frac( cellUv ) - 0.5 );
float2 lw      = 1.0 * deriv;
float2 cov     = saturate( ( wrapped - ( 0.5 - lw ) ) / max( lw, 0.0001 ) );
float  grid    = max( cov.x, cov.y );
```

Классический антиалиасинговый трюк:

- `cellUv` — координаты в единицах «клеток».
- `frac(cellUv) - 0.5` — расстояние от центра клетки (по модулю).
- `ddx/ddy` дают «насколько `cellUv` меняется между соседними пикселями» — это и есть толщина линии в 1 пиксель.
- `saturate((wrapped - (0.5 - lw)) / lw)` плавно даёт `1` на границе клетки и `0` в её центре — без ступенек, в любой ориентации.

Без `ddx/ddy` сетка моргала бы ужасно при отдалении камеры.

### 3. Затухания

```hlsl
// прямоугольный fade по краям зоны
float2 edgeDist = HalfExtents - abs( facePos );
float  edgeFade = saturate( min( edgeDist.x, edgeDist.y )
                            / max( CellSize * 0.5, 0.001 ) );

// круговое затухание от курсора
float dist     = length( p - AimPoint );
float gridFade = 1.0 - saturate( dist / ( MaskRadius * 0.3 ) );
grid *= gridFade;

float mask = edgeFade;
```

Получаем «сетка ярче возле курсора, мягко затухает на краях квадрата».

### 4. Синий крестик в углу привязки

```hlsl
float2 cornerFace = float2( SnapCornerX, SnapCornerY ) * CellSize;
float2 localUv    = facePos - cornerFace;

float2 facePosDdx = ddx( facePos );
float2 facePosDdy = ddy( facePos );
float2 px         = max( abs( facePosDdx ), abs( facePosDdy ) );

float onCenterX = 1.0 - saturate( abs( SnapCornerX ) );
float onCenterY = 1.0 - saturate( abs( SnapCornerY ) );
float barW = lerp( px.x * 1.5, px.x * 2.5, onCenterX );
float barH = lerp( px.y * 1.5, px.y * 2.5, onCenterY );

float barX = saturate( ( barH - abs( localUv.y ) ) / max( barH, 0.0001 ) );
float barY = saturate( ( barW - abs( localUv.x ) ) / max( barW, 0.0001 ) );

float2 aimOffset2 = float2( dot( AimPoint - GridOrigin, GridRight ),
                            dot( AimPoint - GridOrigin, GridUp ) );
float fadeRadius = max( MaskRadius * 0.075, 0.001 );
float fadeVert  = 1.0 - saturate( abs( facePos.y - aimOffset2.y ) / fadeRadius );
float fadeHoriz = 1.0 - saturate( abs( facePos.x - aimOffset2.x ) / fadeRadius );

float cross = saturate( barY * SnapAxisX * fadeVert + barX * SnapAxisY * fadeHoriz );
```

- Строим «крест» из вертикальной и горизонтальной линий.
- Толщина **в пикселях** (через производные) — крест всегда одинаковой ширины на экране.
- Если крест на центральной оси — он чуть толще (`onCenterX`/`onCenterY`).
- Каждая линия креста **затухает** в стороны от курсора (`fadeVert/fadeHoriz`).

### 5. Финальный микс

```hlsl
float4 col = GridColor * grid;

// толстые центр + границы
float2 thickLw = max( abs( facePosDdx ), abs( facePosDdy ) ) * 2.5;
float centerX = saturate( ( thickLw.x - abs( facePos.x ) ) / max( thickLw.x, 0.0001 ) );
float centerY = saturate( ( thickLw.y - abs( facePos.y ) ) / max( thickLw.y, 0.0001 ) );
float centerLines = max( centerX, centerY ) * edgeFade * gridFade;

float2 boundsEdgeDist = abs( HalfExtents - abs( facePos ) );
float boundsX = saturate( ( thickLw.x - boundsEdgeDist.x ) / max( thickLw.x, 0.0001 ) );
float boundsY = saturate( ( thickLw.y - boundsEdgeDist.y ) / max( thickLw.y, 0.0001 ) );
float boundsLines = max( boundsX, boundsY ) * gridFade;

float specialLines = saturate( centerLines + boundsLines );
col = lerp( col, CenterLineColor, specialLines * CenterLineColor.a );
col = lerp( col, CornerColor,     saturate( cross * 2.0 ) );

col.a *= mask;

if ( col.a < 0.002 ) discard;
return col;
```

- Базовый цвет — `GridColor` × сила сетки.
- Поверх — толстые центральные/граничные линии.
- Поверх — синий крест.
- Альфа домножается на «маску прямоугольника».
- Почти-прозрачные пиксели **дискардятся** (для производительности).

## C# часть (как параметры подаются)

В Sandbox код snap-grid находится в `Code/Weapons/ToolGun/SnapGrid` (см. [09_05](09_05_SnapGrid.md)). Он:

1. Создаёт `GameObject` с `ModelRenderer` (квадратный плоский меш).
2. Назначает ему материал на основе `snap_grid.shader`.
3. Каждый кадр пишет в материал актуальные значения через `SceneObject.Attributes.Set(...)`:
   ```csharp
   var attr = renderer.SceneObject.Attributes;
   attr.Set( "GridOrigin",  origin );
   attr.Set( "GridRight",   right );
   attr.Set( "GridUp",      up );
   attr.Set( "AimPoint",    hit.EndPosition );
   attr.Set( "CellSize",    cellSize );
   attr.Set( "HalfExtents", new Vector2( extent, extent ) );
   attr.Set( "SnapCornerX", snapCornerX );
   attr.Set( "SnapCornerY", snapCornerY );
   attr.Set( "SnapAxisX",   axisX );
   attr.Set( "SnapAxisY",   axisY );
   ```
4. Когда инструмент отключён — уничтожает GameObject.

## Чему учит этот шейдер

1. **Без текстур и без освещения** — простой Forward. Не всё графика — это PBR.
2. **`ddx/ddy` для антиалиасинга** — без них тонкие линии моргают на расстоянии.
3. **2D-арифметика на грани в 3D** через `dot(offset, axis)` — всё считаем в плоских координатах.
4. **Render states важны** — `DepthEnable=false` + alpha-blend = чистый overlay.
5. **`Attribute(...)` + `Default*(...)`** — «контракт» между шейдером и C#-кодом, который двигает курсор/сетку.

## Результат

После этого этапа ты:

- ✅ Можешь читать построчно `snap_grid.shader`.
- ✅ Понимаешь, зачем нужны `ddx/ddy` и почему без них сетка моргает.
- ✅ Понимаешь связку «C# пишет `Attributes` → шейдер их читает каждый пиксель».
- ✅ Видел рабочий пример overlay-шейдера, который можно скопировать как стартовый шаблон.

---

📚 **Официальная документация Facepunch:** [shaders/index.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/shaders)

**Следующий шаг:** [28.14 — PostProcess: связь с Фазой 18](28_14_PostProcess_Shaders.md)
