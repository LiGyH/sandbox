# 28.15 — Свой пост-эффект: BlitMode, RenderTarget, регистрация

## Что мы делаем?

Учимся писать **свой** пост-эффект с нуля: HLSL-шейдер + C#-компонент + регистрация на камере. Берём за образец `SniperScopeEffect` из Sandbox’а и проходим путь от «пустого репо» до рабочего эффекта на экране.

## Шаг 1. Шейдер

Создаём `Assets/shaders/postprocess/my_effect.shader`:

```hlsl
HEADER
{
    Description = "My custom screen effect";
    DevShader   = true;
}

MODES
{
    Default();
    VrForward();
}

COMMON
{
    #include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 vPositionOs : POSITION  < Semantic( PosXyz ); >;
    float2 vTexCoord   : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
    float2 vTexCoord : TEXCOORD0;

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
        o.vPositionPs = float4( i.vPositionOs.xy, 0, 1 );
        o.vTexCoord   = i.vTexCoord;
        return o;
    }
}

PS
{
    float Strength < Attribute( "Strength" ); Default( 1.0 ); >;

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        // BackbufferTexture / SampleSceneColor приходит из postprocess/shared.hlsl
        float3 col = Tex2D( g_tColorBuffer, i.vTexCoord ).rgb;

        // инвертируем, смешиваем с оригиналом по Strength
        float3 inv = 1.0 - col;
        col = lerp( col, inv, Strength );

        return float4( col, 1 );
    }
}
```

Что важно:

- **`#include "postprocess/shared.hlsl"`** даёт нам `g_tColorBuffer` (текущий backbuffer) и хелперы.
- **VS просто проецирует full-screen quad** в clip-space (позиция уже в `[-1..1]` приходит от движка).
- **PS читает `g_tColorBuffer` по UV** и пишет результат.

После сохранения файла рядом появится `my_effect.shader_c` (если включён авто-компилятор шейдеров).

## Шаг 2. C#-компонент

`Code/Effects/MyEffect.cs`:

```csharp
using Sandbox.Rendering;

public sealed class MyEffect : BasePostProcess<MyEffect>
{
    [Property, Range( 0f, 1f )]
    public float Strength { get; set; } = 1f;

    private static Material _cachedMaterial;

    public override void Render()
    {
        _cachedMaterial ??= Material.FromShader( "shaders/postprocess/my_effect.shader" );

        Attributes.Set( "Strength", Strength );

        var blit = BlitMode.WithBackbuffer( _cachedMaterial,
            Stage.AfterPostProcess, priority: 200, isHdr: false );

        Blit( blit, "MyEffect" );
    }
}
```

- **`BasePostProcess<MyEffect>`** — стандартный паттерн (см. 28.14).
- **`Material.FromShader(...)`** + **кэширование** в `static`-поле — чтобы материал создавался один раз на всё приложение.
- **`Attributes.Set(...)`** — параметры шейдера на этот кадр.
- **`BlitMode.WithBackbuffer`** — описание blit’а (см. ниже).
- **`Blit( blit, name )`** — отправить blit в очередь рендера.

## BlitMode — что это и какие бывают

`BlitMode` — структура, описывающая **один полный пост-проход** (full-screen quad через шейдер из источника в приёмник). Главные фабрики:

| Фабрика | Что делает |
|---|---|
| `BlitMode.WithBackbuffer( mat, stage, priority, isHdr )` | Источник = текущий backbuffer, приёмник = новый временный RT (движок сам подменит backbuffer). Самый частый случай. |
| `BlitMode.WithRenderTarget( source, target, mat, stage, priority )` | Явно указываешь src/dst. Для многопроходных эффектов (сначала downsample, потом blur, потом composite). |
| `BlitMode.Custom(...)` | Полностью своя функция выполнения — для совсем нестандартных вещей. |

Параметры:

- **`stage`** — `Stage.AfterPostProcess` / `BeforePostProcess` / ... — где встать в конвейере (см. 28.14).
- **`priority`** — порядок внутри стадии (выше число = позже).
- **`isHdr`** — `true`, если хочешь работать в HDR (до тонмаппинга). На `AfterPostProcess` обычно `false`.

## Многопроходный эффект (downsample → blur → composite)

Для серьёзного эффекта (например, кастомный bloom):

```csharp
public override void Render()
{
    // 1. downsample backbuffer в RT 1/2 разрешения
    var halfRT = RenderTarget.GetTemporary( "halfRT", scale: 0.5f, depth: false );
    var downsampleBlit = BlitMode.WithRenderTarget(
        source: BackbufferRT, target: halfRT,
        material: _downsampleMat,
        Stage.AfterPostProcess, 100, isHdr: true );
    Blit( downsampleBlit, "DownsampleHalf" );

    // 2. blur по горизонтали
    var blurXRT = RenderTarget.GetTemporary( "blurX", scale: 0.5f, depth: false );
    Blit( BlitMode.WithRenderTarget( halfRT, blurXRT, _blurXMat,
        Stage.AfterPostProcess, 101, true ), "BlurX" );

    // 3. blur по вертикали
    var blurYRT = RenderTarget.GetTemporary( "blurY", scale: 0.5f, depth: false );
    Blit( BlitMode.WithRenderTarget( blurXRT, blurYRT, _blurYMat,
        Stage.AfterPostProcess, 102, true ), "BlurY" );

    // 4. композит — добавить на backbuffer
    Attributes.Set( "BloomTex", blurYRT );
    Blit( BlitMode.WithBackbuffer( _compositeMat,
        Stage.AfterPostProcess, 103, false ), "BloomComposite" );
}
```

Ключевые приёмы:

- **`RenderTarget.GetTemporary(...)`** — берём временный RT из пула (сам освободится в конце кадра).
- **`scale: 0.5f`** — половина разрешения, вдвое быстрее. Для blur’а почти не теряем качества.
- **`Attributes.Set( "BloomTex", blurYRT )`** — передаём предыдущий RT в финальный шейдер как текстуру.

## Регистрация и время жизни

`BasePostProcess<T>` сам регистрируется при добавлении на `GameObject` с активной камерой. Никаких ручных вызовов `RegisterEffect(...)` не нужно — это делает базовый класс через статический реестр в `Sandbox.Rendering`.

Обычно эффект кладут:

- На **камеру** игрока (для эффектов от первого лица: scope, низкое HP, тепловизор).
- На **отдельный GameObject** в сцене (для глобальных эффектов).

Включение/выключение — через `gameObject.Enabled` или `Enabled` самого компонента.

## Привязка к игровой логике

Пример из `SniperScopeEffect`:

```csharp
public float BlurInput { get; set; }     // 0..1, ставит код снайперки
private float _smoothedBlur;             // плавно догоняем целевое значение

public override void Render()
{
    _smoothedBlur = _smoothedBlur.LerpTo( BlurInput, Time.Delta * 8f );
    var blurAmount = (1.0f - _smoothedBlur).Clamp( 0.1f, 1f );

    Attributes.Set( "BlurAmount", blurAmount );
    ...
}
```

Внешний код (логика снайперки) ставит «цель» — `BlurInput`. Эффект сам сглаживает значение. Это даёт **плавный** scope без рывков, даже если игрок резко отжал ADS.

## Подсказки на практике

- **Кэшируй `Material`** — `Material.FromShader` дорогой.
- **HDR/SDR — серьёзно.** В `AfterPostProcess` (`isHdr=false`) ты работаешь со значениями `0..1`. До `AfterPostProcess` (`isHdr=true`) — со значениями вплоть до 1000+.
- **Не плоди временные RT каждый кадр** — используй `RenderTarget.GetTemporary` (он переиспользует).
- **Имена `Attributes.Set("name", ...)`** должны совпадать с `Attribute("name")` в шейдере. Это самая частая ошибка.
- **Включай эффект только при необходимости** (`Enabled = false`, когда не нужен) — лишние пост-проходы съедают GPU.

## Результат

После этого этапа ты знаешь:

- ✅ Полный путь: `.shader` → `.shader_c` → `Material.FromShader` → `BasePostProcess` → `Blit(BlitMode, name)`.
- ✅ Что такое `BlitMode.WithBackbuffer` / `WithRenderTarget` и когда их использовать.
- ✅ Как делать многопроходные эффекты с `RenderTarget.GetTemporary`.
- ✅ Как привязать пост-эффект к игровой логике с плавным сглаживанием.

---

📚 **Официальная документация Facepunch:** [rendering/post-processing.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/rendering)

**Следующий шаг:** [28.16 — Отладка шейдеров](28_16_Shader_Debugging.md)
