# 28.14 — Пост-обработка: PostProcessResource, PostProcessManager и порядок проходов

## Что мы делаем?

Связываем уже знакомые из [Фазы 18](18_01_PostProcessResource.md) **PostProcessResource** и **PostProcessManager** с тем, что мы узнали про шейдеры. Здесь становится понятно, как кастомные пост-эффекты Sandbox’а (в том числе `sniper_scope`) встраиваются в общий конвейер рендера.

## Что такое пост-обработка

Сцена сначала рисуется «как есть» в backbuffer (текстура размером с экран). Дальше идут **пост-проходы**, каждый из которых:

1. Берёт текущий backbuffer (и иногда depth, scene color, прошлые кадры).
2. Прогоняет через шейдер свою функцию каждого пикселя.
3. Записывает результат обратно (часто через временные render target’ы).

Стандартные пост-эффекты движка (Bloom, Tonemapping, ColorCorrection, Vignette, MotionBlur, DOF, FilmGrain, ChromaticAberration) реализованы внутри s&box и подключаются через **`PostProcessResource`**.

## Recap: PostProcessResource (Фаза 18.01)

`PostProcessResource` — это **ассет** (`.postprocess`), описывающий «эффект» в виде набора параметров: ссылка на шейдер/материал, дефолтные значения, какие-то метаданные UI. Sandbox использует это как «банк эффектов», которые можно включать/настраивать прямо в игре через UI Effects (Spawn Menu → вкладка эффектов).

Подробнее в [18.01 — PostProcessResource](18_01_PostProcessResource.md).

## Recap: PostProcessManager (Фаза 18.02)

`PostProcessManager` — это **`GameObjectSystem`**, живущий в сцене. Он:

- Хранит активные эффекты (выбранные игроком в UI или включённые кодом).
- Слушает `Game.ActiveScene` и подтаскивает соответствующие компоненты (`Sandbox.Bloom`, `Sandbox.Tonemapping` и т.д.) на камеру.
- Сериализует выбранный набор в кукки игрока — настройки переживают рестарт.

В Sandbox’ном UI это вкладка эффектов в SpawnMenu. Пользователь:

1. Выбирает `PostProcessResource` из списка.
2. Крутит ползунки.
3. Менеджер мгновенно применяет.

Подробнее в [18.02 — PostProcessManager](18_02_PostProcessManager.md).

## Где вписывается **свой** пост-шейдер?

`PostProcessResource` — это про **готовые эффекты движка** (Bloom, Tonemap, …). Когда тебе нужен **свой** уникальный эффект, не покрываемый стандартными — ты пишешь:

1. **`.shader`** в `Assets/shaders/postprocess/имя.shader`.
2. **`Component`**, наследующий `BasePostProcess<T>`. Этот компонент сам встраивается в конвейер камеры.

Готовый пример в Sandbox — `Code/Weapons/Sniper/SniperScopeEffect.cs`:

```csharp
using Sandbox.Rendering;

public sealed class SniperScopeEffect : BasePostProcess<SniperScopeEffect>
{
    public float BlurInput { get; set; }

    private float _smoothedBlur;
    private static Material _cachedMaterial;

    public override void Render()
    {
        _smoothedBlur = _smoothedBlur.LerpTo( BlurInput, Time.Delta * 8f );
        var blurAmount = (1.0f - _smoothedBlur).Clamp( 0.1f, 1f );

        Attributes.Set( "BlurAmount", blurAmount );
        Attributes.Set( "Offset", Vector2.Zero );

        _cachedMaterial ??= Material.FromShader( "shaders/postprocess/sniper_scope.shader" );
        var blit = BlitMode.WithBackbuffer( _cachedMaterial,
            Stage.AfterPostProcess, 200, false );
        Blit( blit, "SniperScope" );
    }
}
```

Важные строки:

- **`BasePostProcess<SniperScopeEffect>`** — наследник, дженерик-параметр = «сам тип» (нужно для статического реестра внутри Sandbox.Rendering).
- **`Render()`** — вызывается каждый кадр на активной камере.
- **`Attributes.Set("...", ...)`** — те же `Attribute(...)` шейдера. Прокидываются в текущий blit.
- **`Material.FromShader(...)`** — на лету склеиваем материал поверх сырого `.shader`.
- **`BlitMode.WithBackbuffer(...)`** — описание прохода: «возьми backbuffer, прогони через материал, в стадии `AfterPostProcess`, с приоритетом 200, без HDR». Подробнее об этом — в [28.15](28_15_Custom_PostProcess.md).
- **`Blit( blit, "SniperScope" )`** — добавить проход в очередь рендера. Имя `"SniperScope"` появится в графическом дебаггере (PIX/RenderDoc) как метка прохода.

## Стадии (Stage) — порядок проходов

`Stage` — это перечисление, **где в конвейере** выполнять твой проход:

| Stage | Когда | Что в backbuffer |
|---|---|---|
| `BeforeOpaque` | до отрисовки непрозрачных мешей | пусто/SkyBox |
| `AfterOpaque`  | после непрозрачных, до прозрачных | сцена без эффектов и UI |
| `AfterTransparent` | после прозрачных | геометрия + прозрачное (вода, стекло) |
| `BeforePostProcess` | до Bloom/Tonemap | scene color (HDR, не скорректированный) |
| `AfterPostProcess` | после всех штатных эффектов | финальная картинка перед UI |
| `AfterUI` | после UI | всё (редко используется) |

Sniper scope ставится в **`AfterPostProcess`** — чтобы блюр прицела работал поверх Bloom’а и тонмапа: пиксели уже в SDR, картинка уже «как мы её видим».

## Приоритет (priority)

`Stage` + **число приоритета** определяют порядок внутри одной стадии. У `sniper_scope` это `200`. Если у тебя в той же стадии будет `MyEffect` с приоритетом `100`, он отработает раньше; с `300` — позже.

Стандартные значения:

- 0..99 — самые ранние (вспомогательные, например, downsample).
- 100..199 — основные эффекты сцены.
- 200..299 — overlay’и (scope, ночное видение).
- 300+ — поверх всего (vignette, gamma).

## Сетевая и сценовая видимость

`BasePostProcess` живёт **только на клиенте** (рендер — клиентская задача). На хосте без рендера он просто не активен.

Видимость отдельным игрокам:

- Создавай эффект на ребёнке `Player` — тогда он только у локального игрока.
- Включай/выключай через `gameObject.Enabled = flag`.

Sniper scope в Sandbox привязан к view-модели снайперки (см. `Code/Weapons/Sniper/`). Когда игрок наводит прицел (ADS) — `BlurInput` плавно растёт; когда отжимает — падает. Никаких RPC: чистая локальная анимация одного float’а.

## PostProcessResource против BasePostProcess — когда что

| Критерий | `PostProcessResource` | `BasePostProcess<T>` |
|---|---|---|
| Цель | Стандартный эффект движка для UI выбора. | Свой шейдер под конкретную игровую механику. |
| Шейдер | Встроенный или свой, оборачиваемый в ассет. | Свой `.shader`, прямо подгружаешь. |
| Управление | Игрок крутит из UI. | Код задаёт каждый кадр. |
| Хранение | `.postprocess`-ассет в репо. | Просто Component на камере. |
| В Sandbox | Bloom, Tonemap, Vignette, … | `SniperScopeEffect`. |

Можно совмещать: одна и та же камера может одновременно иметь Bloom + Tonemap + ColorGrading из менеджера и отдельный SniperScope-компонент.

## Подсказки на практике

- **Не наследуй `BasePostProcess`** для вещей, которые уже есть в стандартных эффектах. Не пиши свой Bloom — возьми готовый.
- **Кэшируй материал**, как делает `SniperScopeEffect` (`_cachedMaterial`). Создание материала каждый кадр — лишние аллокации.
- **`Attributes.Set` каждый кадр** — иначе пост-эффект «замёрзнет» на старых параметрах.
- **Имя в `Blit(blit, "имя")`** придумай уникальное — в RenderDoc/PIX оно сильно помогает находить нужный проход.

## Результат

После этого этапа ты знаешь:

- ✅ Как `PostProcessResource` и `PostProcessManager` (Фаза 18) сочетаются с собственными пост-шейдерами.
- ✅ Что `BasePostProcess<T>.Render()` — твоя точка входа в конвейер.
- ✅ Что такое `Stage` и приоритет — и как они задают порядок.
- ✅ Видел разбор `SniperScopeEffect` — реальный пост-эффект Sandbox’а.

---

📚 **Официальная документация Facepunch:** [rendering/post-processing.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/rendering)

**Следующий шаг:** [28.15 — Свой пост-эффект: BlitMode, RenderTarget, регистрация](28_15_Custom_PostProcess.md)
