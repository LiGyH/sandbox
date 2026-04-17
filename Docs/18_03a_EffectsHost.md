# 18.03a — Effects UI: EffectsHost (корневой контейнер)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)
> - [00.27 — Razor стилизация](00_27_Razor_Styling.md)

## Что мы делаем?

Создаём **корневой контейнер** вкладки «Effects» в меню спавна — он размещает список эффектов и панель их свойств рядом друг с другом.

> Это первая из трёх частей урока 18.03. Дальше: [18.03b — EffectsList](18_03b_EffectsList.md), [18.03c — EffectsProperties](18_03c_EffectsProperties.md).

## Код EffectsHost.razor

### Полный код

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox
@attribute [SpawnMenuHost.SpawnMenuMode]
@attribute [Icon( "🎨" )]
@attribute [Title( "Effects" )]
@attribute [Order( -75 )]

<root>
    <EffectsList />
    <EffectsProperties />
</root>
```

### Разбор кода

#### Директивы

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox
```

- `@using` — импорт пространств имён (аналог `using` в C#).
- `@inherits Panel` — компонент наследует `Panel`, базовый класс всех UI-элементов в s&box.
- `@namespace Sandbox` — компонент находится в пространстве имён `Sandbox`.

#### Атрибуты

```razor
@attribute [SpawnMenuHost.SpawnMenuMode]
@attribute [Icon( "🎨" )]
@attribute [Title( "Effects" )]
@attribute [Order( -75 )]
```

- `SpawnMenuHost.SpawnMenuMode` — регистрирует этот компонент как вкладку в меню спавна. Движок сканирует все классы с этим атрибутом и добавляет их как вкладки.
- `Icon("🎨")` — иконка вкладки (палитра художника).
- `Title("Effects")` — заголовок вкладки.
- `Order(-75)` — порядок сортировки среди вкладок. Отрицательное значение — ближе к началу.

#### Разметка

```razor
<root>
    <EffectsList />
    <EffectsProperties />
</root>
```

Внутри `<root>` размещены два дочерних компонента. Они будут отображаться бок о бок (горизонтально), благодаря `flex-direction: row` в SCSS.

---

## Файл 2: EffectsHost.razor.scss

### Полный код

```scss
@import "/UI/Theme.scss";

EffectsHost
{
    position: absolute;
    top: $deadzone-y;
    right: $deadzone-x;
    left: $deadzone-x;
    bottom: 150px;
    width: auto;
    height: auto;
    pointer-events: none;
    flex-direction: row;
    justify-content: center;
    align-items: flex-end;
    gap: 2rem;
    z-index: 10000000;

    EffectsList
    {
        width: 250px;
        height: 500px;
        pointer-events: all;
    }

    EffectsProperties
    {
        width: 500px;
        pointer-events: all;
    }
}
```

### Разбор кода

- **`@import "/UI/Theme.scss"`** — подключает общую тему оформления с переменными (`$deadzone-y`, `$deadzone-x` и др.).
- **`position: absolute`** — панель позиционируется абсолютно внутри родительского контейнера.
- **`top/right/left/bottom`** — отступы от краёв экрана. Переменные `$deadzone-*` задают безопасную зону, чтобы не перекрывать системные элементы.
- **`pointer-events: none`** — сам контейнер не перехватывает клики (прозрачен для мыши), но дочерние элементы (`EffectsList`, `EffectsProperties`) восстанавливают `pointer-events: all`.
- **`flex-direction: row`** — дочерние элементы располагаются горизонтально.
- **`justify-content: center`** — центрирование по горизонтали.
- **`align-items: flex-end`** — выравнивание по нижнему краю.
- **`gap: 2rem`** — промежуток между списком и панелью свойств.
- **`z-index: 10000000`** — очень высокий z-index, чтобы панель была поверх остальных элементов.
- **`EffectsList`** — фиксированная ширина 250px и высота 500px.
- **`EffectsProperties`** — ширина 500px (высота определяется содержимым).

---

---

## ➡️ Следующий шаг

Переходи к **[18.03b — EffectsUI: EffectsList](18_03b_EffectsList.md)**.
