# Этап 14_08 — SpawnMenu

## Что делает этот компонент

**SpawnMenu** — это главная панель меню спавна, которая объединяет всё вместе. Представьте себе **ящик с инструментами, разделённый на два отделения**: левая сторона — для выбора предметов для спавна (пропы, сущности, дубликаты), а правая — для утилит и настроек инструментов.

Меню автоматически находит все зарегистрированные вкладки через систему типов и организует их в табы с плавными анимациями переключения.

---

## Полный исходный код

### SpawnMenu.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits Panel
@attribute [SpawnMenuHost.SpawnMenuMode]
@attribute [Icon( "📦" )]
@attribute [Title( "Spawn" )]
@attribute [Order( -100 )]

<root>

    <div class="container">

        <div class="spawnmenuleft">

            <div class="tabs">

                @foreach (var t in tabs)
                {
                    var type = Game.TypeLibrary.GetType(t.GetType());

                    var c = activeTab == t ? "active" : "";

                    <div class="@c" @onclick=@(() => SwitchTab(t))>
                        <span>@type.Icon</span>
                        <span>@type.Title</span>
                    </div>
                }

            </div>

            <div class="body" @ref="TabContainer">
            </div>

        </div>

        <div class="spawnmenuright">

            <div class="tabs">

                @foreach (var t in utilityTabs)
                {
                    var type = Game.TypeLibrary.GetType(t.GetType());

                    var c = activeUtilityTab == t ? "active" : "";

                    <div class="@c" @onclick=@(() => SwitchUtilityTab(t))>
                        <span>@type.Icon</span>
                        <span>@type.Title</span>
                    </div>
                }

            </div>

            <div class="body" @ref="UtilityContainer">
            </div>

        </div>

    </div>

</root>

@code
{
    Panel activeTab;
    Panel TabContainer = default;

    Panel activeUtilityTab;
    Panel UtilityContainer = default;

    List<Panel> tabs = new List<Panel>();
    List<Panel> utilityTabs = new List<Panel>();

    public void BuildTabs()
    {
        foreach (var p in Game.TypeLibrary.GetTypes<ISpawnMenuTab>().OrderBy(x => x.Order).ThenBy(x => x.Title))
        {
            if (p.IsAbstract) continue;
            if (p.TargetType == typeof(BaseSpawnMenu)) continue;

            var panel = p.Create<Panel>();
            TabContainer.AddChild(panel);

            tabs.Add(panel);
        }

        SwitchTab( RestoreTab( tabs, "spawnmenu.tab" ) );
    }

    public void BuildUtilityTabs()
    {
        foreach (var p in Game.TypeLibrary.GetTypes<IUtilityTab>().OrderBy(x => x.Order).ThenBy(x => x.Title))
        {
            if (p.IsAbstract) continue;

            var panel = p.Create<Panel>();
            UtilityContainer.AddChild(panel);

            utilityTabs.Add(panel);
        }

        SwitchUtilityTab( RestoreTab( utilityTabs, "spawnmenu.utilitytab" ) );
    }

    Panel RestoreTab( List<Panel> panels, string cookieKey )
    {
        var saved = Game.Cookies.Get<string>( cookieKey, null );
        if ( !string.IsNullOrEmpty( saved ) )
        {
            var match = panels.FirstOrDefault( p => p.GetType().Name == saved );
            if ( match is not null ) return match;
        }
        return panels.FirstOrDefault();
    }

    protected override void OnAfterTreeRender(bool firstTime)
    {
        if (firstTime && TabContainer.IsValid())
        {
            BuildTabs();
        }

        if (firstTime && UtilityContainer.IsValid())
        {
            BuildUtilityTabs();
        }
    }

    void SwitchTab(Panel tab)
    {
        activeTab = tab;
        Game.Cookies.Set( "spawnmenu.tab", tab?.GetType().Name );
        StateHasChanged();

        foreach (var t in tabs)
        {
            t.SetClass("active", t == tab);
            t.SetClass("hidden", t != tab);
        }
    }

    void SwitchUtilityTab(Panel tab)
    {
        activeUtilityTab = tab;
        Game.Cookies.Set( "spawnmenu.utilitytab", tab?.GetType().Name );
        StateHasChanged();

        foreach (var t in utilityTabs)
        {
            t.SetClass("active", t == tab);
            t.SetClass("hidden", t != tab);
        }
    }

    // When clicking anywhere in the spawn menu, blur any focused text inputs
    // so we can close the menu by pressing the spawnmenu key.
    void OnMenuClicked()
    {
        Sandbox.UI.InputFocus.Clear();
    }
}
```

### SpawnMenu.razor.scss

```scss
@import "/UI/Theme.scss";

SpawnMenu
{
    width: 100%;
    height: 100%;
    color: $menu-color;
    justify-content: center;
    font-family: $body-font;
    font-size: 14px;
    font-weight: 600;
    z-index: 1000;
}

SpawnMenu .container
{
    width: 100%;
    height: 100%;
    max-width: 1900px;
}

SpawnMenu .container .spawnmenuleft
{
    transform: translateX( -100px );
    transition: all 0.2s ease-in;
}

SpawnMenu .container .spawnmenuright
{
    transform: translateX( 100px );
    transition: all 0.2s ease-in;
}

SpawnMenuHost.open SpawnMenu
{
    .container .spawnmenuleft
    {
        transform: translateX( 0px );
        transition: all 0.1s ease-out;
    }

    .container .spawnmenuright
    {
        transform: translateX( 0px );
        transition: all 0.1s ease-out;
    }
}

.container .spawnmenuleft
{
    flex-grow: 1;
    padding: 128px 64px;
    padding-right: 16px;

    PanelSwitcher
    {
        padding-bottom: 128px;
    }
}

.container .spawnmenuright
{
    width: 700px;
    flex-shrink: 0;
    flex-grow: 0;
    padding: 128px 64px;
    padding-left: 16px;
}

.container > .spawnmenuleft,
.container > .spawnmenuright
{
    flex-direction: column;
}


.container > .spawnmenuleft > .body,
.container > .spawnmenuright > .body
{
    border-radius: 8px;
    flex-grow: 1;
}

.tabs
{
    font-size: 14px;
    color: white;
    font-weight: 650;
    font-family: $subtitle-font;
    margin-left: 8px;
    z-index: 10;
    flex-shrink: 0;
}

.tabs > *
{
    padding: 0.5rem 1rem;
    cursor: pointer;
    color: #D1D7E3AA;
    border-radius: 8px 8px 0 0;
    background-color: #0c202faa;
    opacity: 1;
    border-right: 1px solid $menu-surface-soft;
    gap: 0.4rem;
    align-items: center;
}

.tabs > *:hover
{
    color: $menu-color;
    sound-in: ui.hover;
}

.tabs > *:active
{
    color: $menu-text-strong;
    sound-in: ui.select;
}

.tabs > *.active
{
    opacity: 1;
    color: $menu-text-strong;
    cursor: pointer;
    background-color: $menu-panel-bg;
    pointer-events: none;
    backdrop-filter: blur( 8px );
}


.container > .spawnmenuleft > .body,
.container > .spawnmenuright > .body
{
    position: relative;
}


.container > .spawnmenuleft > .body > *,
.container > .spawnmenuright > .body > *
{
    position: absolute;
    height: 100%;
    width: 100%;
}

.container > .spawnmenuleft > .body > .hidden,
.container > .spawnmenuright > .body > .hidden
{
    opacity: 0;
    pointer-events: none;
    transition: all 0.1s linear;
    transform: translateY( 20px );
}

.container > .spawnmenuleft > .body > .active,
.container > .spawnmenuright > .body > .active
{
    opacity: 1;
    transition: all 0.1s linear;
    transform: translateY( 0px );
}

.container > .spawnmenuleft,
.container > .spawnmenuright
{
    > .body
    {
        background-color: $menu-panel-bg;
        backdrop-filter: blur( 2px );
    }
}


.menuinner
{
    background-color: #13161a88;
    border-radius: 8px;

    h1, h2
    {
        text-transform: uppercase;
        font-size: 11px;
        letter-spacing: 0.1em;
        opacity: 0.5;
        font-weight: 800;
        padding: 8px 4px 2px 4px;
    }
}

.spawnmenuright .body
{
    .tab
    {
        flex-grow: 1;
        flex-direction: row;

        > .left
        {
            flex-grow: 0;
            flex-shrink: 0;
            flex-direction: column;
            width: 180px;
            padding: 8px;

            .list
            {
                flex-direction: column;
                background-color: #2225;
                padding: 0.5rem;
                border-radius: 6px;
                height: 100%;
            }
        }

        > .body
        {
            flex-direction: column;
            flex-grow: 1;
            padding: 8px;
            margin: 8px;
            overflow-y: scroll;
        }

        ControlSheet .body
        {
            padding: 0;
            margin: 0;
        }

        ControlSheet .controlgroup
        {
            padding: 0;

            > .body
            {
                padding: 0;
            }
        }

        ControlSheetGroup
        {
            > .body
            {
                padding: 0;
                margin: 0;
                gap: 2px;
            }
        }

        .page
        {
            gap: 2px;
        }

        controlsheetrow, .control-row
        {
            width: 100%;
            background-color: #53698111;
            padding: 8px;
            border-radius: 4px;
            max-height: auto;

            &:hover
            {
                background-color: #53698122;

                > .left
                {
                    color: #fff;
                }
            }

            > .left
            {
                width: 150px;
                overflow: hidden;
                flex-shrink: 0;
            }

            > .right, > .left
            {
                flex-shrink: 0;
            }
        }
    }
}

VirtualGrid
{
    width: 100%;
    height: 100%;
    gap: 5px;

    .cell
    {
        background-color: #7771;
        border-radius: $hud-radius-sm;
        border: 1px solid transparent;
        cursor: pointer;

        &:hover
        {
            background-color: $menu-accent-soft;
            border-color: rgba($color-accent, 0.4);
        }

        &:active
        {
            background-color: $menu-accent;
            padding: 2px;
        }
    }
}
```

---

## Разбор важных частей кода

### Атрибуты компонента

- `[SpawnMenuHost.SpawnMenuMode]` — регистрирует эту панель как режим меню спавна.
- `[Icon( "📦" )]` — иконка для отображения в системе.
- `[Title( "Spawn" )]` — заголовок панели.
- `[Order( -100 )]` — отрицательный порядок означает, что эта панель отображается первой.

### Структура макета

- **Контейнер** разделён на две части: `.spawnmenuleft` (левая, растягивается) и `.spawnmenuright` (правая, фиксированная ширина 700px).
- Каждая часть содержит полоску **вкладок** (`.tabs`) сверху и **тело** (`.body`) снизу.
- Левая часть использует вкладки типа `ISpawnMenuTab`, правая — `IUtilityTab`.

### Автоматическое обнаружение вкладок (`BuildTabs` / `BuildUtilityTabs`)

- `Game.TypeLibrary.GetTypes<ISpawnMenuTab>()` — находит все типы, реализующие интерфейс `ISpawnMenuTab`.
- Типы сортируются по `Order`, затем по `Title`.
- Абстрактные типы и тип `BaseSpawnMenu` пропускаются.
- Для каждого типа создаётся экземпляр панели и добавляется в контейнер.
- Этот подход позволяет **расширять меню без изменения кода** — достаточно создать новый класс, реализующий `ISpawnMenuTab`.

### Запоминание выбранной вкладки (`RestoreTab`)

- При переключении вкладки её имя сохраняется в `Game.Cookies` (локальное хранилище).
- При открытии меню `RestoreTab` ищет сохранённую вкладку по имени типа.
- Если сохранённая вкладка не найдена — выбирается первая доступная.

### Переключение вкладок (`SwitchTab` / `SwitchUtilityTab`)

- Устанавливает активную вкладку и сохраняет выбор в cookies.
- Вызывает `StateHasChanged()` для перерисовки интерфейса.
- Активная вкладка получает класс `"active"`, остальные — `"hidden"`.
- В стилях `.hidden` делает панель невидимой (`opacity: 0`) со сдвигом вниз, а `.active` — видимой с плавной анимацией.

### Анимации открытия/закрытия

- При закрытом меню левая часть сдвинута на -100px влево, правая — на 100px вправо.
- Когда `SpawnMenuHost` получает класс `.open`, обе части плавно возвращаются на место.
- Открытие быстрее (0.1s `ease-out`), закрытие медленнее (0.2s `ease-in`) — это создаёт ощущение отзывчивости.

### Обработка кликов (`OnMenuClicked`)

- Снимает фокус с текстовых полей (`InputFocus.Clear()`).
- Это нужно, чтобы клавиша открытия меню спавна работала даже когда курсор находится в поле поиска.

### Стилизация вкладок

- Неактивные вкладки полупрозрачные с тёмным фоном.
- При наведении подсвечиваются и проигрывают звук `ui.hover`.
- При нажатии проигрывают звук `ui.select`.
- Активная вкладка имеет фон, совпадающий с телом панели (`$menu-panel-bg`), создавая эффект «продолжения».

### Виртуальная сетка (`VirtualGrid`)

- Используется для отображения элементов (пропов, моделей) в виде сетки.
- Ячейки (`.cell`) имеют скруглённые углы, прозрачную рамку.
- При наведении — мягкая подсветка акцентным цветом.
- При нажатии — более яркий акцент с лёгким уменьшением внутренних отступов (эффект «вдавливания»).
