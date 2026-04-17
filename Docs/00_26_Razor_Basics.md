# 00.26 — Razor Panels: основы UI

## Что мы делаем?

Разбираем **Razor Panels** — главный способ делать UI в s&box. Если ты пишешь меню, HUD, инвентарь, чат, скорборд — это почти всегда Razor. Вся Фаза 5 (HUD), Фаза 6 (Inventory UI), Фаза 14 (SpawnMenu), Фазы 19–21 (Inspector и редакторы) опираются на это.

## Что такое Razor в s&box

Razor — это синтаксис HTML/CSS **со встраиванием C#**. Файлы с расширением `.razor`. Движок их компилирует в нормальный C# класс и рендерит через свою систему UI (не HTML-движок — это просто похожий синтаксис для удобства).

**CSS** у нас не CSS, а SCSS — с переменными, вложенностью. Стилизация — в отдельном `.razor.scss` файле.

## Два уровня — PanelComponent и Panel

| Класс | Что это |
|---|---|
| `PanelComponent` | **Корень UI**. Это компонент (навешивается на `GameObject`). Обязательно нужен рядом `ScreenPanel` или `WorldPanel` — он решает «куда рисовать: на экран или в мир». |
| `Panel` | Любая **дочерняя** часть UI. Не компонент, живёт только внутри UI-дерева. |

По смыслу: `PanelComponent` — как `<html>`, `Panel` — как `<div>` внутри него.

## Создание PanelComponent

В редакторе: **Create → New Razor Panel Component**. Получишь что-то вроде:

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent

<root>
    <div class="title">@Title</div>
</root>

@code
{
    [Property] public string Title { get; set; } = "Hello!";

    protected override int BuildHash() => System.HashCode.Combine( Title );
}
```

Разбор:
- `@inherits PanelComponent` — это корень.
- `<root>` — корневой контейнер. Всё внутри = содержимое панели.
- `@code { ... }` — обычный C# код.
- `@Title` — встраивание переменной C# в HTML.

Чтобы эта панель появилась на экране:
1. Добавь на любой `GameObject` компонент `ScreenPanel`.
2. Добавь рядом твой `MyPanelComponent`.

## Дочерние Panel-ы

```razor
@using Sandbox;
@using Sandbox.UI;

<root>
    <div class="health">HP: @Health</div>
    <div class="armor">Armor: @Armor</div>
</root>

@code
{
    public int Health { get; set; } = 100;
    public int Armor { get; set; } = 50;

    protected override int BuildHash() => System.HashCode.Combine( Health, Armor );
}
```

Это обычный `.razor` (не `PanelComponent`). В другом месте можно его использовать как тег:

```razor
<StatusBar Health=@(50) Armor=@(25) />
```

## Встраивание C# в HTML

```razor
<root>
    @* просто выражение *@
    <div>@player.Name</div>

    @* if *@
    @if ( player.IsDead )
    {
        <img src="ui/skull.png" />
    }

    @* foreach *@
    @foreach ( var p in Scene.GetAllComponents<Player>() )
    {
        <div class="row">@p.Name : @p.Kills</div>
    }

    @* выражение со сложной логикой — оборачивают в скобки *@
    <div style="width: @(progress * 100)%"></div>
</root>
```

## `BuildHash()` — когда перерисовывать

Razor не умный. Он **не** сам понимает, что `@Health` поменялся. По умолчанию он перерисовывает панель только если:

1. `BuildHash()` вернул другое значение.
2. На панель кликнули мышкой / мышка навелась.
3. Ты явно вызвал `StateHasChanged()`.

Поэтому в `BuildHash()` комбинируй **все переменные**, от которых зависит вывод:

```csharp
protected override int BuildHash() =>
    System.HashCode.Combine( Health, Armor, IsDead, PlayerCount );
```

Если забыл — UI будет показывать старые значения.

## Привязки (Binds)

Хочешь, чтобы слайдер менял твою переменную и наоборот? `:bind`:

```razor
<SliderEntry min="0" max="100" step="1" Value:bind=@Volume></SliderEntry>

@code
{
    public int Volume { get; set; } = 50;
}
```

Теперь:
- Слайдер ↔ `Volume` синхронизируются в обе стороны.
- Перерисовка панели не нужна — всё автоматом.

## Ссылка на дочернюю панель

Нужно программно менять её свойства?

```razor
<root>
    <StatusBar @ref="_statusBar" />
</root>

@code
{
    StatusBar _statusBar;

    protected override void OnStart()
    {
        _statusBar.Health = 42;
    }
}
```

## Panel vs PanelComponent: ключевые отличия

| Признак | `PanelComponent` | `Panel` |
|---|---|---|
| Это Component? | ✅ Да | ❌ Нет |
| `OnStart`, `OnUpdate` | ✅ Есть | ❌ Нет (есть `Tick()`, `OnAfterTreeRender`) |
| Может быть корнем UI? | ✅ Да | ❌ Нет |
| Можно `<Foo />` внутри Panel? | ❌ Нельзя (только внутри PanelComponent) | ✅ Можно |
| `Style.Left = ...` | Через `Panel.Style.Left = ...` | Напрямую `Style.Left = ...` |

## Панели в мире vs на экране

- **`ScreenPanel`** — UI поверх всего, в 2D-координатах экрана. HUD, меню.
- **`WorldPanel`** — UI в **3D-мире**. На двери табличка, над NPC имя, на пульте экран.

Оба добавляются как отдельные компоненты, твой `PanelComponent` нарисуется в том, который он находит рядом.

## Ранний возврат

В Razor можно написать `return;` чтобы прекратить рендер:

```razor
<root>
    @if ( !IsVisible ) { return; }
    <div>видимо</div>
</root>
```

Всё после `return` отрисовано не будет.

## Результат

После этого этапа ты знаешь:

- ✅ Что `.razor` файлы — это C#-компоненты UI с HTML-похожим синтаксисом.
- ✅ Разницу между `PanelComponent` (корень) и `Panel` (дочерний).
- ✅ Как встраивать C# в разметку (`@expr`, `@if`, `@foreach`).
- ✅ Почему нужен `BuildHash()` и чем грозит его отсутствие.
- ✅ Как сделать двустороннюю привязку через `:bind`.
- ✅ Что `ScreenPanel` рисует на экран, `WorldPanel` — в мир.

---

📚 **Facepunch docs:** [ui/razor-panels/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/ui/razor-panels/index.md)

**Следующий шаг:** [00.27 — Стилизация Razor Panels (SCSS)](00_27_Razor_Styling.md)
