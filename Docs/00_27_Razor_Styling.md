# 00.27 — Стилизация Razor Panels (SCSS)

## Что мы делаем?

Учимся **оформлять** панели через SCSS. В Фазе 5 («Hud.scss и Theme.scss») ты увидишь горы стилей — без этого этапа они непонятны.

## Конвенция — парный файл

```
Health.razor           ← разметка + C# код
Health.razor.scss      ← стили, автоматически подключены к Health.razor
```

Если файлы лежат рядом и имеют одинаковое имя (с суффиксом `.razor.scss`), движок **автоматически** подключает SCSS к панели. Не надо ничего указывать руками.

## Переопределить путь — `[StyleSheet]`

Если стили лежат в другом месте:

```csharp
@code
{
    [StyleSheet( "/ui/shared/main.scss" )]
    public partial class Health : Panel { }
}
```

Или в SCSS можно импортировать другой SCSS:

```scss
@import "buttons.scss";
```

## SCSS — главное отличие от CSS

SCSS (**Sassy CSS**) — расширение CSS:
- **Вложенность** правил.
- **Переменные** ($name).
- **Миксины** (`@mixin`, `@include`).
- Компилируется в обычный CSS.

Пример:

```scss
$color-primary: #ff8800;
$padding: 12px;

.health-bar {
    background: black;
    padding: $padding;

    .fill {
        background: $color-primary;

        &.low {
            background: red;
        }
    }

    .text {
        color: white;
        font-size: 20px;
    }
}
```

Без вложенности это выглядело бы:

```css
.health-bar { background: black; padding: 12px; }
.health-bar .fill { background: #ff8800; }
.health-bar .fill.low { background: red; }
.health-bar .text { color: white; font-size: 20px; }
```

## Ключевые концепции `Theme.scss` / `Hud.scss`

В Sandbox обычно два файла:

- **`Theme.scss`** — переменные: цвета, размеры, типографика. Импортируется отовсюду.
- **`Hud.scss`** — собственно стили HUD-а, построенные на переменных темы.

```scss
// Theme.scss
$bg-primary: rgba(0, 0, 0, 0.8);
$accent: orange;
$font-main: "Poppins", sans-serif;

// Hud.scss
@import "Theme.scss";

.hud {
    font-family: $font-main;

    .vital {
        background: $bg-primary;
        color: $accent;
    }
}
```

Смысл: поменяешь `$accent: orange;` на `$accent: red;` — **весь HUD** перекрасится.

## Flexbox — главный инструмент вёрстки

s&box UI **не поддерживает** `grid` или `float`. **Всё верстается через flexbox**. Это надо принять.

```scss
.toolbar {
    flex-direction: row;       // расположить детей в ряд
    align-items: center;       // по центру вертикально
    justify-content: flex-end; // прижать к правому краю
    gap: 8px;                  // расстояние между детьми
    padding: 12px;
}
```

`flex-direction`: `row` (в ряд) или `column` (в колонку).

`flex` у детей:

```scss
.main {
    flex-grow: 1;       // займёт всё свободное пространство
}
```

## Что поддерживает UI движок

| Свойство | Поддержка |
|---|---|
| `width`, `height`, `min-*`, `max-*` | ✅ |
| `padding`, `margin`, `border` | ✅ |
| `background`, `background-image` | ✅ |
| `color`, `font-*` | ✅ |
| Flex (`flex-*`, `align-*`, `justify-*`) | ✅ |
| `position: absolute` | ✅ |
| `transform: translate/scale/rotate` | ✅ |
| `transition`, `animation` | ✅ |
| `filter: blur/brightness` | ✅ |
| `border-radius`, `box-shadow` | ✅ |
| `grid`, `float` | ❌ **НЕТ** |
| `:hover`, `:active`, `:focus` | ✅ |

## Пример HUD-стиля

```razor
@* Health.razor *@
<root>
    <div class="hp @(IsLow ? "low" : "")">
        <div class="fill" style="width: @(HpPercent)%"></div>
        <label>@Current / @Max</label>
    </div>
</root>
```

```scss
// Health.razor.scss
.hp {
    position: absolute;
    bottom: 40px;
    left: 40px;
    width: 300px;
    height: 32px;
    background: rgba( 0, 0, 0, 0.6 );
    border-radius: 4px;
    overflow: hidden;

    .fill {
        height: 100%;
        background: linear-gradient( to right, #4caf50, #8bc34a );
        transition: width 0.2s;
    }

    label {
        position: absolute;
        top: 0; left: 0; right: 0; bottom: 0;
        color: white;
        font-size: 18px;
        text-align: center;
        align-items: center;
        justify-content: center;
    }

    &.low .fill {
        background: red;
    }
}
```

## Стилизация прямо в разметке

Для одноразовых штук `style="..."`:

```razor
<label style="color: red; font-size: 32px">DANGER!</label>

<div class="bar">
    <div class="fill" style="width: @(Progress * 100)%"></div>
</div>
```

## Блок `<style>` внутри разметки

Можно писать SCSS прямо в `.razor`:

```razor
<style>
    MyPanel {
        width: 100%;
        height: 100%;
    }
    .hp { color: red; }
</style>

<root>
    <div class="hp">100</div>
</root>
```

Используй для небольших уникальных панелей. Для серьёзных HUD — отдельный `.razor.scss`.

## Изменение стилей кодом

```csharp
myPanel.Style.Width = Length.Pixels( 200 );
myPanel.Style.BackgroundColor = Color.Red;
myPanel.Style.Left = Length.Percent( 50 );
```

Обрати внимание на `Length.*` — это обёртка вокруг чисел с единицами (px, %, vh, auto).

## Результат

После этого этапа ты знаешь:

- ✅ Парный файл `Foo.razor` + `Foo.razor.scss` подключается автоматически.
- ✅ Что такое SCSS и чем он удобнее CSS (переменные, вложенность).
- ✅ Все UI s&box верстаются через **flexbox** (grid/float нет).
- ✅ Как построить тему через переменные (`Theme.scss`).
- ✅ Как менять стили кодом через `Style.*`.

---

📚 **Facepunch docs:**
- [ui/styling-panels/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/ui/styling-panels/index.md)
- [ui/styling-panels/style-properties.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/ui/styling-panels/style-properties.md)

**Следующий шаг:** [00.28 — HudPainter (немедленный рендер HUD)](00_28_HudPainter.md)
