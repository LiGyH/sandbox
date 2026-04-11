# Этап 14_09 — SpawnMenuHost

## Что это такое?

**SpawnMenuHost** — это главный хост-компонент, который управляет открытием и закрытием меню спавна, а также переключением между режимами (Spawn, Context, Save).

### Аналогия

Представьте **пульт от телевизора**: он контролирует, какой канал (режим) сейчас отображается, и обрабатывает кнопку включения/выключения. SpawnMenuHost — это именно такой пульт для всего игрового меню.

---

## Исходный код

### SpawnMenuHost.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

<root @onmousedown=@OnMenuClicked>

    <div class="menu-wrapper">
        <div class="menu-backdrop"></div>
        <SpawnMenuModeBar></SpawnMenuModeBar>
        <div class="menu-slide">
            <PanelSwitcher @ref="PanelSwitcher"></PanelSwitcher>
        </div>
    </div>

</root>

@code
{
    bool stickOpen = false;
    bool lastState;
    string _lastMode = "SpawnMenu";

    PanelSwitcher PanelSwitcher = default;

    protected override void OnUpdate()
    {
        UpdateSpawnMenuState();
        ScanForNewModes();

        var activeType = GetActiveMode()?.GetType().Name;
        SetClass( "no-backdrop", activeType is "ContextMenuHost" or "SaveMenu" or "EffectsHost" );

        base.OnUpdate();
    }

    void UpdateSpawnMenuState()
    {
        var openSpawnMenu = Input.Down("spawnmenu");
        var openInspectMenu = Input.Down("inspectmenu");
        var openAnyMenu = openSpawnMenu || openInspectMenu;

        stickOpen = stickOpen || (Sandbox.UI.InputFocus.Current?.Ancestors.Contains(Panel) ?? false);

        if (stickOpen && Input.Pressed("spawnmenu")) stickOpen = false;

        bool state = stickOpen || openAnyMenu;

        SetClass("open", state);

        if (lastState != state )
        {
            // closed
            if (!state)
            {
                Popup.CloseAll();
            }

            // opened
            if (state)
            {
                if (Input.Pressed("inspectmenu"))
                {
                    Hints.Current.Cancel("openinspectmenu");
                    SwitchMode("ContextMenuHost", false);
                    PanelSwitcher.SkipTransitions();
                }

                if (Input.Pressed("spawnmenu"))
                {
                    Hints.Current.Cancel("openspawnmenu");
                    SwitchMode(_lastMode, false);
                    PanelSwitcher.SkipTransitions();
                    Sandbox.Services.Stats.Increment( "menu.spawnmenu.open", 1 );
                }
            }
        }
        else if ( state )
        {
            // menu stays open — switch modes if the other key is pressed
            if (Input.Pressed("inspectmenu"))
            {
                SwitchMode("ContextMenuHost", false);
            }
            else if (Input.Pressed("spawnmenu"))
            {
                SwitchMode(_lastMode, false);
            }
        }



        lastState = state;
    }

    // When clicking anywhere in the spawn menu, blur any focused text inputs
    // so we can close the menu by pressing the spawnmenu key.
    void OnMenuClicked()
    {
        Sandbox.UI.InputFocus.Clear();
    }

    void ScanForNewModes()
    {
        if ( PanelSwitcher == null )
            return;

        var modes = Game.TypeLibrary.GetTypesWithAttribute<SpawnMenuMode>().OrderBy( x => x.Type.Name != "SpawnMenu" ).ToList();

        // Remove panels whose condition is no longer met
        foreach ( var child in PanelSwitcher.Children.ToList() )
        {
            var mode = modes.FirstOrDefault( m => m.Type.TargetType == child.GetType() );
            if ( mode.Type != null && !mode.Attribute.CheckCondition() )
            {
                if ( PanelSwitcher.ActivePanel == child )
                    SwitchMode( "SpawnMenu" );
                child.Delete();
            }
        }

        // Create panels for modes whose condition is met
        foreach ( var type in modes )
        {
            if ( !type.Attribute.CheckCondition() ) continue;
            if ( PanelSwitcher.Children.Any( x => x.GetType() == type.Type.TargetType ) ) continue;

            var panel = type.Type.Create<Panel>();
            if ( panel == null ) continue;

            PanelSwitcher.AddChild( panel );
        }
    }

    public static void SwitchMode( string modeName, bool remember = true )
    {
        var host = Game.ActiveScene.Get<SpawnMenuHost>();
        if (host?.PanelSwitcher == null) return;

        if (remember) host._lastMode = modeName;

        foreach ( var child in host.PanelSwitcher.Children )
        {
            if ( child.GetType().Name == modeName )
            {
                host.PanelSwitcher.SwitchToPanel(child);
                return;
            }
        }
    }

    public static Panel GetActiveMode()
    {
        var host = Game.ActiveScene.Get<SpawnMenuHost>();
        if (host?.PanelSwitcher == null) return null;
        return host.PanelSwitcher.ActivePanel;
    }

    public interface ISpawnMenuCondition
    {
        static abstract bool IsVisible();
    }

    public class SpawnMenuMode : System.Attribute
    {
        public virtual bool CheckCondition() => true;
    }

    public class SpawnMenuMode<T> : SpawnMenuMode where T : ISpawnMenuCondition
    {
        public override bool CheckCondition() => T.IsVisible();
    }

    public class HostOnly : ISpawnMenuCondition
    {
        public static bool IsVisible() => Networking.IsHost;
    }
}
```

### SpawnMenuHost.razor.scss

```scss
@import "/UI/Theme.scss";

SpawnMenuHost
{
	position: absolute;
	top: 0;
	left: 0;
	right: 0;
	bottom: 0;
	color: $menu-color;
	justify-content: center;
	font-family: $body-font;
	font-size: 14px;
	font-weight: 600;
	z-index: 1000;
	pointer-events: none;
}

SpawnMenuHost .menu-wrapper
{
	width: 100%;
	height: 100%;
	opacity: 0;
	pointer-events: none;
	transition: opacity 0.15s ease-in;
}

SpawnMenuHost.open .menu-wrapper
{
	opacity: 1;
	pointer-events: all;
	transition: opacity 0.15s ease-out;
}

SpawnMenuHost .menu-backdrop
{
	position: absolute;
	top: 0;
	left: 0;
	right: 0;
	bottom: 0;
	backdrop-filter: brightness( 0.4 ) contrast( 0.7 ) saturate( 0.7 );
	transition: opacity 0.15s ease;
}

SpawnMenuHost.no-backdrop .menu-backdrop
{
	opacity: 0;
}

SpawnMenuHost .menu-slide
{
	position: absolute;
	top: 0;
	left: 0;
	right: 0;
	bottom: 0;
	transform: translateX( -60px );
	transition: transform 0.15s ease-in;
}

SpawnMenuHost.open .menu-slide
{
	transform: translateX( 0px );
	transition: transform 0.15s ease-out;
}
```

### ISpawnMenuTab.cs

```csharp
public interface ISpawnMenuTab
{

}
```

---

## Разбор важных частей кода

### Структура Razor-разметки

- **`<root @onmousedown=@OnMenuClicked>`** — корневой элемент; при клике мышью снимает фокус с текстовых полей, чтобы меню можно было закрыть клавишей.
- **`<div class="menu-backdrop">`** — полупрозрачный фон-затемнение, который появляется при открытии меню.
- **`<SpawnMenuModeBar>`** — панель выбора режимов (вкладки сверху).
- **`<PanelSwitcher>`** — компонент-переключатель, который показывает только одну панель (режим) за раз.

### Управление состоянием открытия/закрытия

- **`stickOpen`** — флаг «залипания»: если пользователь кликнул внутри меню (например, в текстовое поле), меню остаётся открытым даже после отпускания клавиши.
- **`Input.Down("spawnmenu")`** — проверяет, удерживает ли игрок клавишу открытия меню спавна.
- **`Input.Down("inspectmenu")`** — проверяет клавишу меню инспекции.
- **`SetClass("open", state)`** — добавляет или убирает CSS-класс `open`, что запускает анимации появления/скрытия через SCSS.

### Переключение режимов

- **`SwitchMode(string modeName, bool remember = true)`** — статический метод, который находит дочернюю панель по имени типа и переключает `PanelSwitcher` на неё.
- **`_lastMode`** — запоминает последний выбранный режим, чтобы при повторном открытии меню вернуться к нему.
- **`PanelSwitcher.SkipTransitions()`** — пропускает анимации переключения при первом открытии, чтобы контент появился мгновенно.

### Динамическое сканирование режимов

- **`ScanForNewModes()`** — каждый кадр сканирует все типы с атрибутом `[SpawnMenuMode]` и автоматически создаёт/удаляет панели.
- **`CheckCondition()`** — позволяет режимам задавать условия видимости (например, `HostOnly` — только для хоста сервера).
- Это означает, что новые режимы можно добавлять просто пометив класс атрибутом — хост подхватит их автоматически.

### Стилизация (SCSS)

- **`pointer-events: none`** — по умолчанию меню не перехватывает клики, оно «прозрачно» для мыши.
- **`SpawnMenuHost.open .menu-wrapper`** — при открытии включаются `pointer-events: all` и `opacity: 1` с плавной анимацией (`transition: opacity 0.15s`).
- **`transform: translateX(-60px)` → `translateX(0px)`** — при открытии контент плавно «въезжает» слева направо.
- **`backdrop-filter: brightness(0.4) contrast(0.7) saturate(0.7)`** — затемняет и обесцвечивает фон игры, чтобы меню было хорошо видно.

### Интерфейс ISpawnMenuTab

- **`ISpawnMenuTab`** — маркерный интерфейс (пустой). Панели, которые хотят быть вкладками меню спавна, реализуют его. Это позволяет системе находить и группировать вкладки.
