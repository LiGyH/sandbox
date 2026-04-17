# 19_02 — Inspector и GameObjectInspector

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

<!-- toc -->
> **📑 Оглавление** (файл большой, используй ссылки для быстрой навигации)
>
> - [Что мы делаем?](#что-мы-делаем)
> - [Как это работает внутри движка](#как-это-работает-внутри-движка)
> - [Путь к файлу](#путь-к-файлу)
> - [Полный код](#полный-код)
> - [Разбор кода](#разбор-кода)
> - [Что проверить](#что-проверить)

## Что мы делаем?

Создаём два компонента:

1. **Inspector** — центральный компонент, который управляет выделением объектов в мире, рисует подсветку-обводку, определяет наведённый объект по лучу камеры и показывает панель инспектора с вкладками.
2. **GameObjectInspector** — одна из вкладок инспектора. Показывает свойства выбранного объекта: массу, здоровье, редактируемые свойства компонентов, материалы и скины моделей.

## Как это работает внутри движка

### Inspector

`Inspector` — это `Panel`, размещённый внутри `ContextMenuHost`. Он делает несколько вещей:

- **Луч из камеры** (`UpdateCursor`): каждый кадр выпускает луч из позиции мыши через `Scene.Camera.ScreenPixelToRay` и находит объект под курсором. Нашедшийся `GameObject` приводится к корневому сетевому объекту через `FindNetworkRoot()`.
- **Выделение** (`SelectObject`): при клике добавляет объект в список `_selected`. Если зажата клавиша `run` (Shift) — мультивыделение; иначе — очистка предыдущего.
- **Подсветка** (`UpdateHighlights`): берёт `HighlightOutline` из `ContextMenuHost` и заполняет их списки рендереров — выделенные объекты рисуются синей обводкой, наведённый — жёлтой.
- **Система вкладок-редакторов** (`InitEditors`, `UpdateEditors`): находит все классы с атрибутом `[InspectorEditor]`, создаёт их экземпляры, размещает в панели `.window-stack` и переключает видимость в зависимости от того, какой редактор может отобразить выбранный набор объектов.

### GameObjectInspector

`GameObjectInspector` реализует интерфейс `IInspectorEditor` и зарегистрирован как вкладка атрибутом `[InspectorEditor(null)]`. Он:

- Получает список выбранных объектов через `TrySetTarget`.
- Перестраивает список свойств (`RebuildFromTarget`): перебирает все компоненты выбранных объектов, ищет свойства с атрибутом `[ClientEditable]`, собирает информацию о материалах у `ModelRenderer`.
- Отображает массу, здоровье, редактируемые свойства, скин модели и материалы с возможностью замены.

## Путь к файлу

```
Code/UI/ContextMenu/Inspector.razor
Code/UI/ContextMenu/Inspector.razor.scss
Code/UI/ContextMenu/GameObjectInspector.razor
Code/UI/ContextMenu/GameObjectInspector.razor.scss
```

## Полный код

### `Code/UI/ContextMenu/Inspector.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits Panel

<root>

	<div class="canvas" @ref=_canvas></div>

	<div class="inspector-panel @(_editors?.Any( e => e.WasVisible ) == true ? "" : "hidden")" @onmousedown="@WindowPress">
		<div class="tab-bar">
			@foreach ( var entry in _editors?.Where( e => e.WasVisible ) ?? [] )
			{
				var e = entry;
				<div class="tab @( _active == e ? "active" : "" )" @onclick="@(() => SetActive( e ))">
					@e.Editor.Title
				</div>
			}
		</div>
		<div class="window-stack" @ref=_windowStack></div>
	</div>

</root>

@code
{
    public GameObject Hovered => hovered;

    Panel _canvas = default;
    Panel _windowStack = default;

    record EditorEntry( IInspectorEditor Editor )
    {
        public bool WasVisible { get; set; }
    }
    List<EditorEntry> _editors;
    EditorEntry _active;

    void InitEditors()
    {
        if ( _editors != null || _windowStack == null ) return;
        _editors = new();

        var types = TypeLibrary.GetTypesWithAttribute<InspectorEditorAttribute>()
            .OrderByDescending( x => x.Type.GetAttribute<OrderAttribute>()?.Value ?? 0 )
            .ThenBy( t => t.Attribute.Type is null ? 1 : 0 );

        foreach ( var (typeDesc, attr) in types )
        {
            var editor = typeDesc.Create<IInspectorEditor>();
            if ( editor is not Panel editorPanel ) continue;

            editorPanel.AddClass( "window" );
            editorPanel.SetClass( "hidden", true );
            editorPanel.Parent = _windowStack;

            _editors.Add( new EditorEntry( editor ) );
        }
    }

    void SetActive( EditorEntry entry )
    {
        _active = entry;
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        foreach ( var e in _editors )
            (e.Editor as Panel)?.SetClass( "hidden", !( e.WasVisible && e == _active ) );
    }

    HashSet<GameObject> _lastSelected = new();

    void UpdateEditors()
    {
        if ( _editors == null ) return;

        bool changed = false;

        foreach ( var entry in _editors )
        {
            bool visible = entry.Editor.TrySetTarget( _selected );
            if ( visible != entry.WasVisible ) changed = true;
            entry.WasVisible = visible;
        }

        if ( !_lastSelected.SetEquals( _selected ) )
        {
            changed = true;
            _lastSelected = _selected.ToHashSet();
        }

        if ( changed )
        {
            var visible = _editors.Where( e => e.WasVisible ).ToList();
            if ( _active == null || !_active.WasVisible )
                _active = visible.LastOrDefault();

            ApplyVisibility();
        }
    }

    protected override int BuildHash() => HashCode.Combine( hovered, _selected.Count, _active );

    public override void Tick()
    {
        _selected.RemoveAll( x => !x.IsValid() );
        InitEditors();
        UpdateEditors();
        UpdateHighlights();
        UpdateCursor();
    }

    protected override void OnVisibilityChanged()
    {
        UpdateHighlights();
    }

    void UpdateHighlights()
    {
        var host = Ancestors.OfType<ContextMenuHost>().FirstOrDefault();
        if (host is null) return;

        if (host.SelectedOutline is null || host.HoveredOutline is null)
            return;

        host.SelectedOutline.Targets ??= new();
        host.SelectedOutline.Targets.Clear();
        host.SelectedOutline.Color = new Color(4.7f, 10.1f, 30.6f, 1);
        host.SelectedOutline.ObscuredColor = new Color(2.2f, 2.3f, 2.9f, 0.1f);
        host.SelectedOutline.Width = 0.2f;

        host.HoveredOutline.Targets ??= new();
        host.HoveredOutline.Targets.Clear();
        host.HoveredOutline.Color = new Color(2.6f, 2.0f, 0.2f, 1);
        host.HoveredOutline.ObscuredColor = new Color(2.6f, 2.0f, 0.2f, 0.1f);
        host.HoveredOutline.Width = 0.2f;

        if (!IsVisible)
            return;

        host.SelectedOutline.Targets.AddRange(_selected.SelectMany(x => GetRenderers( x ) ) ?? []);

        if ( !_selected.Contains( hovered  ) )
        {
            host.HoveredOutline.Targets = GetRenderers( Hovered ).ToList();
        }
        else
        {
            host.HoveredOutline.Targets = default;
        }
    }

    IEnumerable<Renderer> GetRenderers( GameObject o )
    {
        if ( o == null ) yield break;

        foreach ( var r in o.GetComponents<Renderer>() )
        {
            yield return r;
        }

        foreach( var c in o.Children )
        {
            if (c.NetworkMode == NetworkMode.Object) continue;

            foreach (var rr in GetRenderers( c ) )
            {
                yield return rr;
            }
        }
    }

    GameObject hovered;

    List<GameObject> _selected = new();

    void UpdateCursor()
    {
        var cursorPos = Mouse.Position;
        var screenRay = Scene.Camera.ScreenPixelToRay( cursorPos );
        var tr = Scene.Trace.Ray(screenRay, 4096 )
                            .IgnoreGameObjectHierarchy( Player.FindLocalPlayer()?.GameObject )
                            .Run();

        var go = tr.Collider?.GameObject ?? tr.GameObject;
        go = go.FindNetworkRoot();

        if (!_canvas.HasHovered) go = default;
        if (!CanSelect(go)) go = null;

        UpdateHovered(go);
    }

    bool CanSelect( GameObject o )
    {
        if (o == null) return false;
        if (o.Tags.Has("world")) return false;
        if (o.NetworkMode == NetworkMode.Never) return false;

        o = o?.FindNetworkRoot();

        return true;
    }

    void UpdateHovered( GameObject o )
    {
        o = o?.FindNetworkRoot();

        if (hovered == o) return;

        hovered = o;
        PlaySound("ui.button.over");
    }


    public void WorldMouseDown(MousePanelEvent e)
    {
        SelectObject(hovered);
    }

    public void WorldMouseRightDown(MousePanelEvent e)
    {
        if ( !hovered.IsValid() ) return;

        SelectObject( hovered );

        var target = hovered;
        var isPlayer = target.Tags.Has( "player" );
        var prop = target.GetComponent<Prop>();
        var isGibbable = prop.IsValid() && prop.Health > 0;

        var menu = MenuPanel.Open( this );
        if ( !isPlayer )
        {
            menu.AddOption( "🗑️", "Delete", () => GameManager.DeleteInspectedObject( target ) );
        }

        if ( isGibbable )
        {
            menu.AddOption( "💥", "Break", () => GameManager.BreakInspectedProp( prop ) );
        }
    }

    public void WorldMouseUp(MousePanelEvent e)
    {
        // nothing.
    }

    public void SelectObject( GameObject o )
    {
        if ( !o.IsValid() )
        {
            _selected.Clear();
        }
        else
        {
            if (!Input.Down("run"))
                _selected.Clear();

            _selected.Remove(o);
            _selected.Add(o);
        }

        _selected = _selected.Distinct().ToList();
    }

	void WindowPress( PanelEvent panelEvent )
	{
		panelEvent.StopPropagation();
	}
}
```

### `Code/UI/ContextMenu/Inspector.razor.scss`

```scss
@import "/UI/Theme.scss";

Inspector
{
    font-size: 1.0rem;
    color: #ddd;
    transition: all 0.2s linear;

    > .canvas
    {
        width: 100%;
        height: 100%;
        z-index: 0;
        position: absolute;
        top: 0px;
        left: 0px;
        pointer-events: all;
    }
}

Inspector .inspector-panel
{
    position: absolute;
    right: $deadzone-x;
    bottom: $deadzone-y;
    width: 500px;
    flex-direction: column;
    pointer-events: none;

    &.hidden
    {
        display: none;
    }
}

Inspector .tab-bar
{
    flex-direction: row;
    margin-left: 8px;
    z-index: 10;
    flex-shrink: 0;
    font-size: 14px;
    font-weight: 650;
    font-family: $subtitle-font;
    pointer-events: all;
}

Inspector .tab
{
    padding: 0.5rem 1rem;
    cursor: pointer;
    color: #D1D7E3AA;
    border-radius: 8px 8px 0 0;
    background-color: #0c202faa;
    border-right: 1px solid $menu-surface-soft;
    gap: 0.4rem;
    align-items: center;
    pointer-events: all;

    &:hover
    {
        color: $menu-color;
        sound-in: ui.hover;
    }

    &.active
    {
        color: $menu-text-strong;
        background-color: $menu-panel-bg;
        pointer-events: none;
        backdrop-filter: blur( 8px );
    }
}

Inspector .window-stack
{
    pointer-events: all;
}

Inspector .window-stack .window
{
    background-color: $menu-panel-bg;
    backdrop-filter: blur( 8px );
    border: 1px solid $menu-surface-soft;
    padding: 1rem;
    width: 500px;
    border-radius: 8px;
    pointer-events: all;
    z-index: 200;

    &.hidden
    {
        display: none;
    }
}

Inspector .window-stack .window .body
{
    flex-direction: column;
    flex-grow: 1;
    overflow-y: scroll;
    gap: 2px;
}

Inspector .window-stack .window .footer
{
    flex-direction: row;
    gap: 6px;
    padding-top: 8px;
    flex-shrink: 0;

    Button
    {
        flex-grow: 1;
    }
}

Inventory.hidden
{
    opacity: 0;
}
```

### `Code/UI/ContextMenu/GameObjectInspector.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@attribute [InspectorEditor(null)]
@attribute [Order(100)]
@inherits Panel
@namespace Sandbox
@implements IInspectorEditor

<root>
    <div class="body">
            @if (Target == null || Target.Count == 0)
            {
                <div class="empty-state">Click on an object to inspect it.</div>
            }
            else
            {
                var totalMass = Target.SelectMany( go => go.GetComponentsInChildren<Rigidbody>() ).Sum( rb => rb.Mass );
                var health = Target.Select( go => go.GetComponent<Prop>() ).FirstOrDefault( p => p.IsValid() );

                <div class="object-info">
                    @if ( totalMass > 0 )
                    {
                        <span>⚖️ @($"{totalMass:0.#} kg")</span>
                    }
                    @if ( health.IsValid() && health.Health != 0 )
                    {
                        <span>❤️ @($"{health.Health:0.#} HP")</span>
                    }
                </div>
                <ControlSheet Target="@Properties"></ControlSheet>

                @if (Renderers.Count > 0)
                {
                    @if ( MaterialGroups.Count > 1 )
                    {
                        var currentGroup = Renderers[0].MaterialGroup ?? MaterialGroups[0];
                        <div class="material-row">
                            <label>Skin</label>
                            <div class="material-button" @onclick="@PickMaterialGroup">
                                <label>@currentGroup</label>
                                <label class="material-group-arrow">▾</label>
                            </div>
                        </div>
                    }

                    var accessor = Renderers[0].Materials;
                    @for (int i = 0; i < accessor.Count; i++)
                    {
                        var index = i;
                        var hasOverride = accessor.HasOverride(index);
                        var mat = hasOverride ? accessor.GetOverride(index) : accessor.GetOriginal(index);
                        var name = mat?.ResourceName ?? "Default";

                        <div class="material-row @(hasOverride ? "overridden" : "")">
                            <label>Material @(index + 1)</label>
                            <div class="material-button" @onclick=@(() => PickMaterial(index))>
                                <div class="material-preview" style="background-image: url( thumb:@(mat?.ResourcePath) )"></div>
                                <label>@name</label>
                            </div>
                            @if (hasOverride)
                            {
                                <div class="material-revert" @onclick=@(() => RevertMaterial(index))>x</div>
                            }
                        </div>
                    }
                }
            }
    </div>
</root>

@code
{
    public string Title => Target?.Count switch
    {
        null or < 2 => "📦 Object",
        _ => $"📦 Object (+{Target.Count - 1})"
    };

    public List<GameObject> Target { get; private set; }

    public bool TrySetTarget(List<GameObject> selection)
    {
        var ids = selection.Select(x => x.Id);
        if (!ids.SequenceEqual(Target?.Select(x => x.Id) ?? []))
        {
            Target = selection.Any() ? selection.ToList() : null;
            RebuildFromTarget();
            StateHasChanged();
        }

        // Hide the tab when something is selected but there's nothing to show
        return Target == null || HasContent();
    }

    bool HasContent()
    {
        if ( Target == null ) return false;
        if ( Properties.Count > 0 || Renderers.Count > 0 ) return true;

        var totalMass = Target.SelectMany( go => go.GetComponentsInChildren<Rigidbody>() ).Sum( rb => rb.Mass );
        if ( totalMass > 0 ) return true;

        var health = Target.Select( go => go.GetComponent<Prop>() ).FirstOrDefault( p => p.IsValid() );
        if ( health.IsValid() && health.Health != 0 ) return true;

        return false;
    }

    List<SerializedProperty> Properties = new();
    List<ModelRenderer> Renderers = new();
    List<string> MaterialGroups = new();

    protected override int BuildHash()
    {
        var hc = new HashCode();
        foreach ( var go in Target ?? [] )
        {
            hc.Add( go.Id );
            hc.Add( go.GetComponent<Rigidbody>()?.Mass ?? 0f );
            hc.Add( go.GetComponent<Prop>()?.Health ?? -1f );
            hc.Add( go.GetComponent<ModelRenderer>()?.MaterialGroup );
        }
        return hc.ToHashCode();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        RebuildFromTarget();
    }

    void RebuildFromTarget()
    {
        Properties = new();
        Renderers = new();
        MaterialGroups = new();

        if (Target == null) return;

        foreach (var c in Target.SelectMany(x => x.Components.GetAll()).Distinct().GroupBy(x => x is Collider ? typeof(Collider) : x.GetType()))
        {
            CollectProperties(c.ToArray());
        }
    }

    bool HasEditableProperties(Type type, PropertyDescription[] properties)
    {
        if (type.IsAssignableTo(typeof(ModelRenderer))) return true;
        if (type.IsAssignableTo(typeof(Collider))) return true;

        foreach (var prop in properties)
        {
            if (prop.HasAttribute<ClientEditableAttribute>())
                return true;
        }

        return false;
    }

    void CollectProperties(Component[] components)
    {
        var firstComponent = components.First();

        var tl = TypeLibrary.GetType(firstComponent.GetType());
        if (tl is null) return;

        if (!HasEditableProperties(firstComponent.GetType(), tl.Properties)) return;

        var so = new MultiSerializedObject();
        so.OnPropertyChanged = PropertyChanged;

        foreach (var component in components)
            so.Add(TypeLibrary.GetSerializedObject(component));

        so.Rebuild();

        foreach (var prop in tl.Properties)
        {
            if (!prop.HasAttribute<ClientEditableAttribute>()) continue;
            Properties.Add(so.GetProperty(prop.Name));
        }

        if (firstComponent is ModelRenderer mr)
        {
            Renderers.AddRange(components.OfType<ModelRenderer>());

            var model = mr.Model;
            if ( model is not null )
            {
                for ( int i = 0; i < model.MaterialGroupCount; i++ )
                    MaterialGroups.Add( model.GetMaterialGroupName( i ) );
            }

            var prop = mr.GetComponent<Prop>();
            if (prop is not null)
            {
                var propso = TypeLibrary.GetSerializedObject(prop);
                propso.OnPropertyChanged = PropertyChanged;
                Properties.Add(propso.GetProperty(nameof(ModelRenderer.Tint)));
            }
            else
            {
                Properties.Add(so.GetProperty(nameof(ModelRenderer.Tint)));
            }

            Properties.Add(so.GetProperty(nameof(ModelRenderer.RenderType)));
        }

        if (firstComponent is Collider)
            Properties.Add(so.GetProperty(nameof(Collider.Surface)));
    }

    void PropertyChanged(SerializedProperty prop)
    {
        foreach (var c in prop.Parent.Targets)
        {
            if (c is Component component)
                GameManager.ChangeProperty(component, prop.Name, prop.GetValue<object>());
        }
    }

    void PickMaterialGroup()
    {
        var menu = MenuPanel.Open( this );
        var current = Renderers[0].MaterialGroup ?? MaterialGroups.FirstOrDefault();
        foreach ( var group in MaterialGroups )
        {
            var g = group;
            menu.AddOption( current == g ? "check" : "", g, () => SetMaterialGroup( g ) );
        }
    }

    void SetMaterialGroup( string group )
    {
        foreach ( var renderer in Renderers )
            GameManager.ChangeProperty( renderer, nameof( ModelRenderer.MaterialGroup ), group );
    }

    void PickMaterial(int index)
    {
        var accessor = Renderers[0].Materials;
        var mat = accessor.HasOverride(index) ? accessor.GetOverride(index) : accessor.GetOriginal(index);

        var popup = new ResourceSelectPopup();
        popup.Extension = "material";
        popup.CurrentValue = mat?.ResourcePath;
        popup.AllowPackages = true;
        popup.Parent = FindPopupPanel();
        popup.OnSelectedFile = (path) => SetMaterialOverride(index, path);
    }

    void SetMaterialOverride(int index, string path)
    {
        foreach (var renderer in Renderers)
            GameManager.ChangeMaterialOverride(renderer, index, path);
    }

    void RevertMaterial(int index)
    {
        foreach (var renderer in Renderers)
            GameManager.ChangeMaterialOverride(renderer, index, null);
    }
}
```

### `Code/UI/ContextMenu/GameObjectInspector.razor.scss`

```scss

GameObjectInspector
{
    flex-direction: column;
    overflow: hidden;
width: 100%;
}

GameObjectInspector .object-info
{
    flex-direction: row;
    gap: 1rem;
    padding-bottom: 0.5rem;
    margin-bottom: 0.25rem;
    font-size: 11px;
    color: rgba( white, 0.45 );

    span
    {
        gap: 0.3rem;
        align-items: center;
    }
}

GameObjectInspector .empty-state
{
    text-align: center;
    color: rgba( white, 0.35 );
    font-size: 12px;
    padding: 1rem 0;
}

GameObjectInspector controlsheetrow
{
    flex-direction: row;
    max-height: auto;
    width: 100%;

    > .right, > .left
    {
        flex-shrink: 0;
    }
}

GameObjectInspector .material-row
{
    flex-direction: row;
    align-items: center;
    gap: 0.25rem;
    padding: 0.15rem 0;
    width: 100%;

    > label
    {
        flex-shrink: 0;
        font-size: 11px;
        color: rgba( white, 0.5 );
        width: 70px;
    }

    .material-button
    {
        width: 100%;
        flex-direction: row;
        align-items: center;
        gap: 0.4rem;
        padding: 0.2rem 0.4rem;
        background-color: rgba( white, 0.05 );
        border-radius: 4px;
        border: 1px solid transparent;
        cursor: pointer;
        overflow: hidden;

        label
        {
            font-size: 11px;
            color: rgba( white, 0.7 );
            text-overflow: ellipsis;
            white-space: nowrap;
            overflow: hidden;
        }

        .material-group-arrow
        {
            flex-shrink: 0;
            margin-left: auto;
            font-size: 10px;
            color: rgba( white, 0.4 );
        }

        .material-preview
        {
            width: 20px;
            height: 20px;
            border-radius: 3px;
            flex-shrink: 0;
            background-size: cover;
            background-position: center;
            background-color: rgba( white, 0.1 );
        }

        &:hover
        {
            background-color: rgba( white, 0.1 );

            label
            {
                color: white;
            }
        }
    }

    .material-revert
    {
        flex-shrink: 0;
        font-size: 11px;
        color: rgba( white, 0.3 );
        cursor: pointer;
        padding: 0.15rem 0.3rem;
        border-radius: 3px;

        &:hover
        {
            color: white;
            background-color: rgba( red, 0.3 );
        }
    }

    &.overridden .material-button
    {
        background-color: rgba( #4CAF50, 0.15 );
        border: 1px solid rgba( #4CAF50, 0.3 );

        label
        {
            color: rgba( #4CAF50, 0.9 );
        }
    }
}
```

## Разбор кода

### Inspector.razor — шаблон

Шаблон состоит из двух частей:

1. **`.canvas`** — невидимый div на весь экран с `pointer-events: all`. Он нужен для того, чтобы определять, наведён ли курсор на «свободную» область мира (через `_canvas.HasHovered`). Если курсор на окне инспектора — мы не хотим менять `hovered`.

2. **`.inspector-panel`** — панель инспектора. Показывается, только если хотя бы один редактор (`_editors`) был видим (`WasVisible`). Содержит:
   - **`.tab-bar`** — строка вкладок. Для каждого видимого редактора рисуется `<div class="tab">` с заголовком. Активная вкладка выделяется классом `active`.
   - **`.window-stack`** — контейнер для окон редакторов. Видимо только активное окно.

Обработчик `@onmousedown="@WindowPress"` вызывает `StopPropagation()` — это не даёт клику по панели инспектора «провалиться» в `ContextMenuHost` и случайно снять выделение с объекта.

### Inspector.razor — `@code`

#### Система редакторов

```csharp
record EditorEntry( IInspectorEditor Editor )
{
    public bool WasVisible { get; set; }
}
```

Каждый редактор оборачивается в `EditorEntry` — запись, хранящую ссылку на редактор и флаг видимости.

**`InitEditors()`** — вызывается однократно. Находит все типы с атрибутом `[InspectorEditor]` через `TypeLibrary`, сортирует по `[Order]`, создаёт экземпляры, добавляет как `Panel` в `.window-stack`.

**`UpdateEditors()`** — каждый кадр вызывает `TrySetTarget(_selected)` для каждого редактора. Если набор выбранных объектов изменился или видимость редакторов поменялась — пересчитывает активную вкладку.

**`ApplyVisibility()`** — скрывает все окна, кроме активного, через CSS-класс `hidden`.

#### Трассировка луча

```csharp
var screenRay = Scene.Camera.ScreenPixelToRay( cursorPos );
var tr = Scene.Trace.Ray(screenRay, 4096)
    .IgnoreGameObjectHierarchy( Player.FindLocalPlayer()?.GameObject )
    .Run();
```

Луч длиной 4096 юнитов, игнорируем объекты самого игрока. Результат — `tr.Collider?.GameObject ?? tr.GameObject`. Обязательно приводим к корневому сетевому объекту: `go.FindNetworkRoot()`.

#### Фильтрация

```csharp
bool CanSelect( GameObject o )
{
    if (o.Tags.Has("world")) return false;      // нельзя выбрать мир
    if (o.NetworkMode == NetworkMode.Never) return false; // только сетевые
    return true;
}
```

#### Выделение

```csharp
public void SelectObject( GameObject o )
```

Если объект невалиден — очищаем список. Если зажат `run` (Shift) — добавляем к существующему выделению (мультивыбор). Иначе — заменяем. `Distinct()` убирает дубликаты.

#### Контекстное меню

При правом клике создаём `MenuPanel` с опциями:
- **Delete** — удаляет объект через `GameManager.DeleteInspectedObject`.
- **Break** — ломает пропс (если у него есть здоровье) через `GameManager.BreakInspectedProp`.

Для игроков (тег `"player"`) кнопка Delete скрыта.

#### Подсветка

`UpdateHighlights()` берёт `HighlightOutline` из родительского `ContextMenuHost` через `Ancestors.OfType<ContextMenuHost>()` и настраивает цвета:
- Выделенные объекты: яркий синий (`4.7, 10.1, 30.6`) — HDR-значения для bloom-эффекта.
- Наведённый объект: жёлтый (`2.6, 2.0, 0.2`).

`GetRenderers()` — рекурсивно собирает все `Renderer` из объекта и его детей, пропуская дочерние объекты с `NetworkMode.Object` (они самостоятельные сетевые сущности).

### GameObjectInspector.razor

#### Атрибуты

```razor
@attribute [InspectorEditor(null)]
@attribute [Order(100)]
@implements IInspectorEditor
```

- `[InspectorEditor(null)]` — регистрирует как редактор общего назначения (`null` — без привязки к конкретному типу).
- `[Order(100)]` — приоритет (чем выше, тем раньше в списке).
- `@implements IInspectorEditor` — обязывает реализовать `TrySetTarget` и `Title`.

#### Заголовок вкладки

```csharp
public string Title => Target?.Count switch
{
    null or < 2 => "📦 Object",
    _ => $"📦 Object (+{Target.Count - 1})"
};
```

Если выбрано несколько объектов — в заголовке показывается количество.

#### `TrySetTarget`

Сравнивает ID объектов в новом выделении с текущим. Если изменилось — перестраивает свойства через `RebuildFromTarget()`. Возвращает `true` (показать вкладку), если нет выделения или есть контент для отображения.

#### `RebuildFromTarget`

Группирует все компоненты выбранных объектов по типу (коллайдеры объединяются в одну группу). Для каждой группы вызывает `CollectProperties`.

#### `CollectProperties`

1. Проверяет, есть ли у типа редактируемые свойства (`HasEditableProperties`).
2. Создаёт `MultiSerializedObject` — обёртку для одновременного редактирования нескольких компонентов одного типа.
3. Собирает свойства с атрибутом `[ClientEditable]`.
4. Для `ModelRenderer` дополнительно добавляет `Tint`, `RenderType`, группы материалов.
5. Для `Collider` — свойство `Surface`.

#### Шаблон рендеринга

- Если ничего не выбрано — показывает текст «Click on an object to inspect it.».
- Если есть выбор — показывает массу (⚖️), здоровье (❤️), `ControlSheet` для редактируемых свойств, скин модели и список материалов.
- Каждый материал можно заменить через `ResourceSelectPopup`.
- Переопределённые материалы помечаются зелёным и имеют кнопку сброса (`x`).

#### `PropertyChanged`

При изменении свойства через UI вызывает `GameManager.ChangeProperty` для каждого целевого компонента — это сетевой вызов, синхронизирующий изменения.

### Стили

#### Inspector.razor.scss

- `.canvas` — невидимый слой на весь экран с `pointer-events: all` для перехвата кликов.
- `.inspector-panel` — абсолютно позиционирована в правом нижнем углу (`right: $deadzone-x; bottom: $deadzone-y`).
- `.tab` — вкладки с полупрозрачным фоном. Активная вкладка — с blur-эффектом.
- `.window` — окно редактора с размытием фона, рамкой и скругленными углами.

#### GameObjectInspector.razor.scss

- `.object-info` — строка с информацией (масса, HP).
- `.material-row` — строка материала: иконка, название, кнопка выбора.
- `.material-button` — кнопка с превью материала.
- `.overridden` — переопределённый материал выделяется зелёным (`#4CAF50`).
- `.material-revert` — кнопка сброса, красная при наведении.

## Что проверить

1. Выберите объект в режиме Inspect — в правом нижнем углу должна появиться панель инспектора с вкладкой «📦 Object».
2. Выберите пропс с массой — должна отобразиться масса (⚖️) и здоровье (❤️).
3. Выберите объект с моделью — должны появиться материалы. Кликните по материалу — откроется попап выбора.
4. Замените материал — строка должна стать зелёной, появится кнопка `x` для сброса.
5. Выберите несколько объектов (Shift+клик) — заголовок вкладки должен показать «📦 Object (+N)».
6. Правый клик по пропсу — должно появиться контекстное меню с «Delete» и «Break».
7. Правый клик по игроку — кнопка «Delete» не должна отображаться.


---

## ➡️ Следующий шаг

Переходи к **[19.03 — InspectorEditor и InspectorEditorAttribute](19_03_InspectorEditor.md)**.
