# 18_03 — Effects UI: интерфейс управления эффектами пост-обработки

## Что мы делаем?

Создаём набор UI-компонентов для вкладки «Effects» в меню спавна. Это три Razor-компонента:

- **EffectsHost** — корневой контейнер, который размещает список и панель свойств рядом друг с другом.
- **EffectsList** — список всех доступных эффектов с вкладками «Installed» и «Workshop», чекбоксами включения, предпросмотром при наведении и системой пресетов.
- **EffectsProperties** — панель редактирования свойств выбранного эффекта через `ControlSheet`.

Вместе они дают игроку полный контроль над визуальными эффектами: просмотр, переключение, тонкая настройка параметров, сохранение/загрузка пресетов.

## Как это работает внутри движка

UI в s&box строится на Razor-компонентах (`.razor`-файлы), стилизуемых через SCSS (`.razor.scss`-файлы). Каждый компонент наследует `Panel` и описывает свою разметку в HTML-подобном синтаксисе. Стили используют переменные темы из `Theme.scss`.

Компоненты общаются через `PostProcessManager` (см. урок 18_02):
- `EffectsList` вызывает методы `Toggle()`, `Select()`, `Preview()`, `Unpreview()`.
- `EffectsProperties` читает `SelectedPath` и `GetSelectedComponents()` для отображения редактора.

Razor поддерживает `@code`-блоки с C#-логикой, привязки событий (`@onclick`, `@onmouseenter`), условный рендеринг (`@if`) и циклы (`@foreach`).

## Путь к файлам

- `Code/UI/Effects/EffectsHost.razor`
- `Code/UI/Effects/EffectsHost.razor.scss`
- `Code/UI/Effects/EffectsList.razor`
- `Code/UI/Effects/EffectsList.razor.scss`
- `Code/UI/Effects/EffectsProperties.razor`
- `Code/UI/Effects/EffectsProperties.razor.scss`

---

## Файл 1: EffectsHost.razor

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

## Файл 3: EffectsList.razor

### Полный код

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    <div class="list-tabs">
        <div class="list-tab @( _tab == 0 ? "active" : "" )" @onclick="@(() => SwitchTab(0))">📦 Installed</div>
        <div class="list-tab @( _tab == 1 ? "active" : "" )" @onclick="@(() => SwitchTab(1))">☁️ Workshop</div>
    </div>

    <div class="panel-body">
        <div class="scroll-body">
            @if ( _tab == 0 )
            {
            @foreach ( var group in _groups )
            {
                <div class="group-header">@group.Key.ToString()</div>

                @foreach ( var entry in group )
                {
                    var path = entry.ResourcePath;
                    var enabled = _manager?.IsEnabled( path ) ?? false;
                    var selected = _manager?.SelectedPath == path;

                    <div class="effect-row @( selected ? "selected" : "" )"
                         @onclick="@(() => OnClick( entry ))"
                         @onmouseenter="@(() => _manager?.Preview( path ))"
                         @onmouseleave="@(() => _manager?.Unpreview())">

                        <div class="thumb emoji">@GroupEmoji( entry.Group )</div>
                        <div class="name">@entry.Title</div>
                        <div class="toggle @( enabled ? "on" : "" )" @onclick:stopPropagation="true" @onclick="@(() => OnToggle( entry ))">
                            <div class="checkmark">✓</div>
                        </div>

                    </div>
                }
            }

            @if ( !_groups.Any() )
            {
                <div class="empty-state">No installed effects.</div>
            }
        }
        else
        {
            <div class="workshop-search">
                <TextEntry class="search-input" placeholder="Search.." value="@_filter"/>
            </div>

            @if ( _loadingWorkshop )
            {
                <div class="empty-state">Loading...</div>
            }
            else if ( _workshopPackages.Count == 0 )
            {
                <div class="empty-state">No results.</div>
            }
            else
            {
                @foreach ( var pkg in _workshopPackages )
                {
                    var mounted = _mountedResources.GetValueOrDefault( pkg.FullIdent );
                    var resourcePath = mounted?.ResourcePath;
                    var enabled = resourcePath != null && (_manager?.IsEnabled( resourcePath ) ?? false);
                    var selected = resourcePath != null && _manager?.SelectedPath == resourcePath;
                    var mounting = _mounting.Contains( pkg.FullIdent );
                    var thumbUrl = mounted?.Icon != null ? $"thumb:{mounted.Icon.ResourcePath}" : pkg.Thumb;

                    <div class="effect-row @( selected ? "selected" : "" )"
                         @onclick="@(() => OnWorkshopClick( pkg ))"
                         @onmouseenter="@(() => { if ( resourcePath != null ) _manager?.Preview( resourcePath ); })"
                         @onmouseleave="@(() => _manager?.Unpreview())">

                        <div class="thumb" style="background-image: url(@thumbUrl)"></div>
                        <div class="info">
                            <div class="name">@pkg.Title</div>
                            <div class="author">
                                <div class="author-avatar" style="background-image: url(@pkg.Org.Thumb)"></div>
                                <span>@pkg.Org.Title</span>
                            </div>
                        </div>
                        <div class="toggle @( enabled ? "on" : "" ) @( mounting ? "loading" : "" )"
                             @onclick:stopPropagation="true" @onclick="@(() => OnWorkshopToggle( pkg ))">
                            <div class="checkmark">@( mounting ? "…" : "✓" )</div>
                        </div>

                    </div>
                }
            }
        }
    </div>

    @if ( _tab == 0 )
    {
        <div class="footer">
            <Button class="menu-action primary" Text="Presets" Icon="📋" onclick=@OpenPresetsMenu></Button>
        </div>
    }
    </div>
</root>

@code
{
    PostProcessManager _manager;
    IGrouping<PostProcessGroup, PostProcessResource>[] _groups = [];
    int _tab = 0;

    // Workshop
    string _filter = "";
    bool _loadingWorkshop;
    List<Package> _workshopPackages = new();
    Dictionary<string, PostProcessResource> _mountedResources = new();
    HashSet<string> _mounting = new();

    protected override void OnAfterTreeRender( bool firstTime )
    {
        if ( !firstTime ) return;

        _manager = Game.ActiveScene.GetSystem<PostProcessManager>();
        RefreshInstalled();
        StateHasChanged();
    }

    void RefreshInstalled()
    {
        _groups = ResourceLibrary.GetAll<PostProcessResource>()
            .OrderBy( r => r.Group )
            .ThenBy( r => r.Title )
            .GroupBy( r => r.Group )
            .ToArray();
    }

    void SwitchTab( int tab )
    {
        _tab = tab;
        if ( tab == 1 && _workshopPackages.Count == 0 )
            _ = FetchWorkshop();
        StateHasChanged();
    }

    async Task FetchWorkshop()
    {
        _loadingWorkshop = true;
        StateHasChanged();

        var query = string.IsNullOrEmpty(_filter) ? "sort:newest" : $"sort:newest {_filter}";
        var result = await Package.FindAsync( query );
        _workshopPackages = result?.Packages.Where( p => p.TypeName == "spp" ).ToList() ?? new();

        _loadingWorkshop = false;
        StateHasChanged();
    }

    void OnFilterInput()
    {
        _ = FetchWorkshop();
    }

    async Task<PostProcessResource> MountAndGet( Package pkg )
    {
        if ( _mountedResources.TryGetValue( pkg.FullIdent, out var cached ) ) return cached;

        _mounting.Add( pkg.FullIdent );
        StateHasChanged();

        var resource = await ResourceLibrary.LoadAsync<PostProcessResource>( pkg.FullIdent );
        resource ??= await Cloud.Load<PostProcessResource>( pkg.FullIdent, true );

        _mounting.Remove( pkg.FullIdent );

        if ( resource != null )
            _mountedResources[pkg.FullIdent] = resource;

        StateHasChanged();
        return resource;
    }

    async void OnWorkshopClick( Package pkg )
    {
        var resource = await MountAndGet( pkg );
        if ( resource is null ) return;
        _manager?.Select( resource.ResourcePath );
        StateHasChanged();
    }

    async void OnWorkshopToggle( Package pkg )
    {
        var resource = await MountAndGet( pkg );
        if ( resource is null ) return;
        _manager?.Toggle( resource.ResourcePath );
        StateHasChanged();
    }

    void OnClick( PostProcessResource entry )
    {
        _manager?.Select( entry.ResourcePath );
        StateHasChanged();
    }

    void OnToggle( PostProcessResource entry )
    {
        _manager?.Toggle( entry.ResourcePath );
        StateHasChanged();
    }

    static string GroupEmoji( PostProcessGroup group ) => group switch
    {
        PostProcessGroup.Effects  => "✨",
        PostProcessGroup.Overlay  => "🎭",
        PostProcessGroup.Shaders  => "⚡",
        PostProcessGroup.Textures => "🧱",
        _                         => "🔧",
    };

    const string PresetsKey = "effects/presets";

    record EffectState( string ResourcePath, Dictionary<string, object> Properties );
    record EffectsPreset( string Name, List<EffectState> Effects );
    record EffectsPresetList( List<EffectsPreset> Presets );

    EffectsPresetList LoadPresetList() => LocalData.Get<EffectsPresetList>( PresetsKey, new EffectsPresetList( new() ) );

    Dictionary<string, object> CaptureProperties( IReadOnlyList<Component> components )
    {
        var result = new Dictionary<string, object>();
        foreach ( var component in components )
        {
            var so = TypeLibrary.GetSerializedObject( component );
            foreach ( var prop in so.Where( p => !p.IsMethod
                && p.PropertyType != null
                && !p.PropertyType.IsAssignableTo( typeof( Delegate ) )
                && p.HasAttribute<PropertyAttribute>() ) )
            {
                try
                {
                    var value = prop.GetValue<object>();
                    if ( value is not null )
                        result[prop.Name] = value;
                }
                catch { }
            }
        }
        return result;
    }

    void ApplyProperties( string resourcePath, Dictionary<string, object> properties )
    {
        foreach ( var component in _manager.GetComponents( resourcePath ) )
        {
            var typeDesc = TypeLibrary.GetType( component.GetType() );
            foreach ( var (name, value) in properties )
            {
                var propDesc = typeDesc?.GetProperty( name );
                if ( propDesc is null ) continue;

                try
                {
                    var resolved = value is System.Text.Json.JsonElement el
                        ? Sandbox.Json.Deserialize( el.GetRawText(), propDesc.PropertyType )
                        : value;
                    if ( resolved is not null )
                        propDesc.SetValue( component, resolved );
                }
                catch { }
            }
        }
    }

    void SaveNewPreset( string name )
    {
        if ( _manager is null ) return;

        var effects = _groups.SelectMany( g => g )
            .Where( r => _manager.IsEnabled( r.ResourcePath ) )
            .Select( r => new EffectState( r.ResourcePath, CaptureProperties( _manager.GetComponents( r.ResourcePath ) ) ) )
            .ToList();

        var list = LoadPresetList();
        list.Presets.RemoveAll( p => p.Name == name );
        list.Presets.Add( new EffectsPreset( name, effects ) );
        LocalData.Set( PresetsKey, list );
        StateHasChanged();
    }

    void ApplyPreset( EffectsPreset preset )
    {
        if ( _manager is null ) return;

        var presetPaths = preset.Effects.Select( e => e.ResourcePath ).ToHashSet();

        foreach ( var group in _groups )
            foreach ( var entry in group )
                if ( !presetPaths.Contains( entry.ResourcePath ) && _manager.IsEnabled( entry.ResourcePath ) )
                    _manager.Toggle( entry.ResourcePath );

        foreach ( var effectState in preset.Effects )
        {
            if ( !_manager.IsEnabled( effectState.ResourcePath ) )
                _manager.Toggle( effectState.ResourcePath );

            ApplyProperties( effectState.ResourcePath, effectState.Properties );
        }

        StateHasChanged();
    }

    void DeletePreset( string name )
    {
        var list = LoadPresetList();
        list.Presets.RemoveAll( p => p.Name == name );
        if ( list.Presets.Count == 0 )
            LocalData.Delete( PresetsKey );
        else
            LocalData.Set( PresetsKey, list );
        StateHasChanged();
    }

    void OpenPresetsMenu()
    {
        var menu = MenuPanel.Open( this );

        menu.AddOption( "save", "Save New Preset...", () =>
        {
            var popup = new StringQueryPopup
            {
                Title = "Save Preset",
                Prompt = "Enter a name for this effects preset.",
                Placeholder = "Preset name...",
                ConfirmLabel = "Save",
                OnConfirm = name => SaveNewPreset( name )
            };
            popup.Parent = FindPopupPanel();
        } );

        var list = LoadPresetList();
        if ( list.Presets.Count > 0 )
        {
            menu.AddSpacer();
            foreach ( var preset in list.Presets )
            {
                var captured = preset;
                menu.AddSubmenu( "auto_awesome", captured.Name, sub =>
                {
                    sub.AddOption( "play_arrow", "Load", () => ApplyPreset( captured ) );
                    sub.AddOption( "delete", "Delete", () => DeletePreset( captured.Name ) );
                } );
            }
        }
    }

    protected override int BuildHash() =>
        HashCode.Combine( _tab, _manager?.SelectedPath, _manager?.IsEnabled( _manager?.SelectedPath ?? "" ), _loadingWorkshop );
}
```

### Разбор кода

#### Поля состояния

```csharp
PostProcessManager _manager;
IGrouping<PostProcessGroup, PostProcessResource>[] _groups = [];
int _tab = 0;
```

- `_manager` — ссылка на `PostProcessManager`, получаемая при первом рендере.
- `_groups` — массив группировок ресурсов по `PostProcessGroup` (Effects, Overlay, Shaders, Textures).
- `_tab` — индекс текущей вкладки: `0` = Installed, `1` = Workshop.

#### Поля Workshop

```csharp
string _filter = "";
bool _loadingWorkshop;
List<Package> _workshopPackages = new();
Dictionary<string, PostProcessResource> _mountedResources = new();
HashSet<string> _mounting = new();
```

- `_filter` — строка поиска по мастерской.
- `_loadingWorkshop` — флаг загрузки (показывает «Loading...»).
- `_workshopPackages` — список пакетов из мастерской.
- `_mountedResources` — кеш подключённых (скачанных) ресурсов.
- `_mounting` — множество пакетов, которые сейчас скачиваются.

#### Инициализация

```csharp
protected override void OnAfterTreeRender( bool firstTime )
{
    if ( !firstTime ) return;
    _manager = Game.ActiveScene.GetSystem<PostProcessManager>();
    RefreshInstalled();
    StateHasChanged();
}
```

`OnAfterTreeRender` вызывается после построения дерева элементов. При первом рендере получаем менеджер, загружаем установленные эффекты и обновляем UI.

#### Загрузка установленных эффектов

```csharp
void RefreshInstalled()
{
    _groups = ResourceLibrary.GetAll<PostProcessResource>()
        .OrderBy( r => r.Group )
        .ThenBy( r => r.Title )
        .GroupBy( r => r.Group )
        .ToArray();
}
```

Получаем все ресурсы типа `PostProcessResource`, сортируем по группе и имени, группируем — получаем массив `IGrouping`.

#### Переключение вкладок

```csharp
void SwitchTab( int tab )
{
    _tab = tab;
    if ( tab == 1 && _workshopPackages.Count == 0 )
        _ = FetchWorkshop();
    StateHasChanged();
}
```

При переключении на Workshop, если пакеты ещё не загружены — запускаем асинхронную загрузку. `_ = FetchWorkshop()` — «fire and forget» вызов `Task`.

#### Загрузка из мастерской

```csharp
async Task FetchWorkshop()
{
    _loadingWorkshop = true;
    StateHasChanged();

    var query = string.IsNullOrEmpty(_filter) ? "sort:newest" : $"sort:newest {_filter}";
    var result = await Package.FindAsync( query );
    _workshopPackages = result?.Packages.Where( p => p.TypeName == "spp" ).ToList() ?? new();

    _loadingWorkshop = false;
    StateHasChanged();
}
```

Используем API `Package.FindAsync` для поиска пакетов. Фильтруем по типу `"spp"` (s&box post-processing). `StateHasChanged()` вызывается до и после загрузки для обновления UI.

#### Монтирование пакета из мастерской

```csharp
async Task<PostProcessResource> MountAndGet( Package pkg )
{
    if ( _mountedResources.TryGetValue( pkg.FullIdent, out var cached ) ) return cached;

    _mounting.Add( pkg.FullIdent );
    StateHasChanged();

    var resource = await ResourceLibrary.LoadAsync<PostProcessResource>( pkg.FullIdent );
    resource ??= await Cloud.Load<PostProcessResource>( pkg.FullIdent, true );

    _mounting.Remove( pkg.FullIdent );

    if ( resource != null )
        _mountedResources[pkg.FullIdent] = resource;

    StateHasChanged();
    return resource;
}
```

Двухэтапная загрузка: сначала пытаемся из `ResourceLibrary` (может быть уже кеширован), затем из `Cloud` (скачивание). `_mounting` используется для отображения индикатора загрузки.

#### Обработчики кликов

```csharp
void OnClick( PostProcessResource entry )
{
    _manager?.Select( entry.ResourcePath );
    StateHasChanged();
}

void OnToggle( PostProcessResource entry )
{
    _manager?.Toggle( entry.ResourcePath );
    StateHasChanged();
}
```

`OnClick` — выбирает эффект (для показа свойств). `OnToggle` — переключает вкл/выкл.

#### Эмоджи групп

```csharp
static string GroupEmoji( PostProcessGroup group ) => group switch
{
    PostProcessGroup.Effects  => "✨",
    PostProcessGroup.Overlay  => "🎭",
    PostProcessGroup.Shaders  => "⚡",
    PostProcessGroup.Textures => "🧱",
    _                         => "🔧",
};
```

Паттерн `switch expression` — возвращает эмоджи для каждой группы эффектов.

#### Система пресетов

Пресеты хранятся в `LocalData` (локальное хранилище клиента) под ключом `"effects/presets"`. Структура:

```csharp
record EffectState( string ResourcePath, Dictionary<string, object> Properties );
record EffectsPreset( string Name, List<EffectState> Effects );
record EffectsPresetList( List<EffectsPreset> Presets );
```

- `EffectState` — состояние одного эффекта: путь + словарь свойств.
- `EffectsPreset` — именованный набор эффектов.
- `EffectsPresetList` — список всех сохранённых пресетов.

#### Сохранение пресета

```csharp
void SaveNewPreset( string name )
```

Собирает все включённые эффекты с их текущими свойствами (`CaptureProperties`), удаляет пресет с таким же именем (если есть) и добавляет новый.

#### Применение пресета

```csharp
void ApplyPreset( EffectsPreset preset )
```

1. Выключает все эффекты, которые **не входят** в пресет.
2. Включает все эффекты из пресета.
3. Применяет сохранённые свойства (`ApplyProperties`).

#### Захват свойств (CaptureProperties)

```csharp
Dictionary<string, object> CaptureProperties( IReadOnlyList<Component> components )
```

Проходит по всем компонентам, извлекает сериализуемые свойства (помеченные `[Property]`), игнорирует делегаты и методы. Результат — словарь «имя свойства → значение».

#### Применение свойств (ApplyProperties)

```csharp
void ApplyProperties( string resourcePath, Dictionary<string, object> properties )
```

Обратная операция: устанавливает значения свойств из словаря. Обрабатывает случай, когда значение — `JsonElement` (если пресет был десериализован из JSON).

#### Меню пресетов

```csharp
void OpenPresetsMenu()
```

Открывает контекстное меню с опцией «Save New Preset...» и списком существующих пресетов. Каждый пресет имеет подменю с пунктами «Load» и «Delete».

#### BuildHash

```csharp
protected override int BuildHash() =>
    HashCode.Combine( _tab, _manager?.SelectedPath, _manager?.IsEnabled( _manager?.SelectedPath ?? "" ), _loadingWorkshop );
```

Определяет, нужно ли перерисовывать компонент. Если хеш изменился с прошлого кадра — компонент перерисовывается.

---

## Файл 4: EffectsList.razor.scss

### Полный код

```scss
@import "/UI/Theme.scss";

EffectsList
{
	flex-direction: column;

	.list-tabs
	{
		flex-direction: row;
		margin-left: 8px;
		flex-shrink: 0;

		.list-tab
		{
			padding: 0.5rem 1rem;
			cursor: pointer;
			color: #D1D7E3AA;
			border-radius: 8px 8px 0 0;
			background-color: #0c202faa;
			border-right: 1px solid $menu-surface-soft;
			align-items: center;
			font-family: $subtitle-font;
			font-weight: 650;
			font-size: 14px;
			transition: color 0.1s ease;

			&:hover
			{
				color: $menu-color;
				sound-in: ui.hover;
			}

			&.active
			{
				color: $menu-text-strong;
				background-color: $menu-panel-bg;
				backdrop-filter: blur( 8px );
				pointer-events: none;
			}
		}
	}

	.panel-body
	{
		background-color: $menu-panel-bg;
		backdrop-filter: blur( 2px );
		border-radius: 8px;
		flex: 1 1 0;
		min-height: 0;
		flex-direction: column;
		overflow: hidden;
	}

	.scroll-body
	{
		flex: 1 1 0;
		min-height: 0;
		flex-direction: column;
		overflow-y: scroll;
	}

	.footer
	{
		flex-shrink: 0;
		flex-direction: row;
		padding: 6px 8px;
		justify-content: flex-end;
	}

	.workshop-search
	{
		padding: 6px 8px;
		.search-input
		{
			width: 100%;
			background-color: rgba( white, 0.06 );
			border: 1px solid rgba( white, 0.1 );
			border-radius: 4px;
			padding: 5px 8px;
			font-size: 1rem;
			font-family: $body-font;
			color: white;
		}
	}

	.group-header
	{
		font-family: $subtitle-font;
		text-transform: uppercase;
		letter-spacing: 1px;
		color: rgba( white, 0.35 );
		margin-top: 8px;
		margin-bottom: 4px;
		margin-left: 12px;
		margin-right: 10px;
		flex-shrink: 0;
	}

	.empty-state
	{
		font-family: $subtitle-font;
		padding: 16px 12px;
		text-align: center;
		color: $menu-text;
	}

	.effect-row
	{
		flex-direction: row;
		align-items: center;
		padding: 6px 8px;
		border-radius: 4px;
		margin: 2px 4px;
		transition: background-color 0.08s ease;
		cursor: pointer;
		flex-shrink: 0;

		.thumb
		{
			width: 28px;
			height: 28px;
			border-radius: 4px;
			background-size: cover;
			background-position: center;
			background-color: rgba( white, 0.05 );
			flex-shrink: 0;

			&.emoji
			{
				background-image: none;
				background-color: rgba( white, 0.07 );
				justify-content: center;
				align-items: center;
				font-size: 1rem;
			}
		}

		.name
		{
			flex-grow: 1;
			font-size: 1rem;
			font-weight: 500;
			font-family: $body-font;
			color: $menu-text;
			margin: 0 8px;
		}

		.info
		{
			flex-grow: 1;
			flex-direction: column;
			justify-content: center;
			margin: 0 8px;

			.name
			{
				flex-grow: 0;
				margin: 0;
			}

			.author
			{
				flex-direction: row;
				align-items: center;
				font-size: 0.9rem;
				font-family: $body-font;
				color: rgba( white, 0.35 );
				margin-top: 2px;

				.author-avatar
				{
					width: 12px;
					height: 12px;
					border-radius: 2px;
					background-size: cover;
					background-position: center;
					background-color: rgba( white, 0.1 );
					margin-right: 4px;
					flex-shrink: 0;
				}
			}
		}

		.toggle
		{
			width: 20px;
			height: 20px;
			border-radius: 4px;
			border: 1.5px solid rgba( white, 0.2 );
			background-color: rgba( black, 0.4 );
			flex-shrink: 0;
			justify-content: center;
			align-items: center;

			.checkmark { font-size: 12px; color: transparent; }

			&.on
			{
				border-color: $color-accent;
				background-color: rgba( $color-accent, 0.2 );
				.checkmark { color: $color-accent; }
			}

			&.loading
			{
				opacity: 0.5;
				pointer-events: none;
				.checkmark { color: rgba( white, 0.5 ); }
			}

			&:hover { border-color: rgba( $color-accent, 0.8 ); }
		}

		&:hover
		{
			background-color: $menu-accent-soft;
			.name { color: $menu-text-strong; }
			.info .name { color: $menu-text-strong; }
		}

		&.selected
		{
			background-color: $menu-accent-soft;
			.name { color: $menu-color; }
			.info .name { color: $menu-color; }
			.thumb { border: 1px solid rgba( $color-accent, 0.4 ); }
		}
	}
}
```

### Разбор кода

Стили организованы по блокам:

- **`.list-tabs`** — горизонтальный ряд вкладок. Неактивная вкладка полупрозрачна, при наведении подсвечивается и воспроизводит звук (`sound-in: ui.hover` — специфичное свойство s&box). Активная вкладка имеет фоновый блюр и не принимает клики.

- **`.panel-body`** — основное тело панели с фоном и блюром. `flex: 1 1 0` + `min-height: 0` — классический трюк Flexbox, чтобы контейнер мог сжиматься и скроллиться.

- **`.scroll-body`** — прокручиваемая область списка.

- **`.effect-row`** — строка эффекта. Состоит из миниатюры (`.thumb`), названия (`.name`) и чекбокса (`.toggle`). При наведении фон меняется, при выборе — добавляется акцентный цвет.

- **`.toggle`** — чекбокс. В выключенном состоянии галочка прозрачна, во включённом — окрашена в акцентный цвет. Состояние `.loading` используется при скачивании пакета из мастерской.

- **`.info`** — блок информации для пакетов из мастерской (название + автор с аватаркой).

- **`.workshop-search`** — поле поиска во вкладке Workshop.

- **`.group-header`** — заголовок группы эффектов (uppercase, мелкий шрифт, приглушённый цвет).

---

## Файл 5: EffectsProperties.razor

### Полный код

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    @if ( _selected.IsValid() )
    {
        <div class="header">
            <h2>
                <span>🎨</span>
                <span>@_selected.Title</span>
            </h2>
        </div>

        @if ( _properties.Count > 0 )
        {
            <ControlSheet Target="@_properties" />
        }
        else
        {
            <div class="empty-state">No editable properties.</div>
        }
    }
    else
    {
        <div class="empty-state">Select an effect to edit its properties.</div>
    }
</root>

@code
{
    PostProcessManager _manager;
    PostProcessResource _selected;
    List<SerializedProperty> _properties = new();

    public override void Tick()
    {
        if ( _manager is null )
            _manager = Game.ActiveScene.GetSystem<PostProcessManager>();

        var selectedPath = _manager?.SelectedPath;
        var resource = selectedPath != null
            ? ResourceLibrary.Get<PostProcessResource>( selectedPath )
            : null;

        if ( resource != _selected )
        {
            _selected = resource;
            RebuildProperties();
        }

        if ( _selected is not null )
            StateHasChanged();
    }

    void RebuildProperties()
    {
        var path = _selected?.ResourcePath;
        if ( path is null )
        {
            _properties = [];
            return;
        }

        var props = new List<SerializedProperty>();
        props.Add( TypeLibrary.CreateProperty( "Enabled",
            () => _manager?.IsEnabled( path ) ?? false,
            v => _manager?.Set( path, v ) ) );

        foreach ( var component in _manager?.GetSelectedComponents() ?? [] )
        {
            var so = TypeLibrary.GetSerializedObject( component );
            so.OnPropertyChanged = OnPropertyChanged;
            props.AddRange( so.Where( FilterProperties ) );
        }

        _properties = props;
    }

    static bool FilterProperties( SerializedProperty o )
    {
        if ( o.PropertyType is null ) return false;
        if ( o.PropertyType.IsAssignableTo( typeof(Delegate) ) ) return false;

        if ( o.IsMethod ) return true;
        if ( !o.HasAttribute<PropertyAttribute>() ) return false;

        return true;
    }

    void OnPropertyChanged( SerializedProperty prop )
    {
        foreach ( var target in prop.Parent.Targets )
        {
            if ( target is Component component )
                GameManager.ChangeProperty( component, prop.Name, prop.GetValue<object>() );
        }
    }

    protected override int BuildHash() => HashCode.Combine( _manager?.SelectedPath );
}
```

### Разбор кода

#### Разметка

Панель показывает одно из двух состояний:
1. **Эффект выбран** (`_selected.IsValid()`) — заголовок с названием и `ControlSheet` для редактирования свойств.
2. **Эффект не выбран** — заглушка «Select an effect to edit its properties.»

`ControlSheet` — встроенный компонент s&box, который принимает список `SerializedProperty` и автоматически создаёт подходящие элементы управления (слайдеры, чекбоксы, цветовые пикеры и т.д.).

#### Метод `Tick()`

```csharp
public override void Tick()
```

Вызывается каждый кадр. Проверяет, изменился ли выбранный эффект. Если да — перестраивает список свойств. Если эффект выбран — вызывает `StateHasChanged()` для обновления значений в реальном времени (слайдеры двигаются → значения обновляются).

#### Метод `RebuildProperties()`

```csharp
void RebuildProperties()
```

Строит список `SerializedProperty`:

1. Первым добавляет виртуальное свойство `"Enabled"` — чекбокс включения/выключения эффекта. Создаётся через `TypeLibrary.CreateProperty` с геттером и сеттером в виде лямбд.
2. Проходит по всем компонентам выбранного эффекта, извлекает сериализованные свойства и фильтрует их.
3. Подписывается на `OnPropertyChanged` для отслеживания изменений.

#### Фильтр свойств

```csharp
static bool FilterProperties( SerializedProperty o )
```

Пропускает:
- Свойства с `null`-типом — пропускает.
- Делегаты — пропускает.
- Методы (кнопки действий) — пропускает **через**.
- Свойства без атрибута `[Property]` — пропускает.

#### Обработка изменений

```csharp
void OnPropertyChanged( SerializedProperty prop )
```

При изменении свойства в UI вызывает `GameManager.ChangeProperty()`, чтобы изменение корректно зарегистрировалось в системе (для undo, синхронизации и т.д.).

---

## Файл 6: EffectsProperties.razor.scss

### Полный код

```scss
@import "/UI/Theme.scss";

EffectsProperties
{
    background-color: $menu-panel-bg;
    backdrop-filter: blur( 2px );
    border-radius: 8px;
    flex-direction: column;
    overflow: hidden;

    .header
    {
        padding: 10px 14px;
        flex-shrink: 0;

        h2
        {
            gap: 0.5rem;
            align-items: center;
            font-size: 14px;
            font-weight: 650;
            font-family: $subtitle-font;
            color: $menu-color;
        }
    }

    .empty-state
    {
        font-family: $subtitle-font;
        padding: 16px 12px;
        text-align: center;
        color: $menu-text;
        flex-grow: 1;
        align-items: center;
        justify-content: center;
    }

    ControlSheet
    {
        padding: 6px 0;
        flex-grow: 1;
        overflow-y: scroll;
    }
}
```

### Разбор кода

- **Корневой элемент `EffectsProperties`** — полупрозрачный фон с блюром и скруглёнными углами. `overflow: hidden` обрезает содержимое по границам.
- **`.header`** — заголовок с названием эффекта. `flex-shrink: 0` — не сжимается при нехватке места.
- **`h2`** — шрифт `$subtitle-font`, акцентный цвет `$menu-color`, `gap: 0.5rem` — отступ между эмоджи и текстом.
- **`.empty-state`** — заглушка, центрированная по обеим осям. `flex-grow: 1` — занимает всё доступное пространство.
- **`ControlSheet`** — `flex-grow: 1` + `overflow-y: scroll` — занимает оставшееся место и прокручивается, если свойств много.

---

## Что проверить

1. **Вкладка в меню спавна** — откройте меню спавна (клавиша Q по умолчанию). Должна появиться вкладка «Effects» с иконкой 🎨.
2. **Список эффектов** — во вкладке «Installed» должны отображаться все установленные `PostProcessResource`, сгруппированные по категориям с эмоджи.
3. **Предпросмотр при наведении** — наведите курсор на эффект в списке. Он должен временно включиться (видно на экране). При уходе курсора — выключиться.
4. **Переключение эффекта** — нажмите на чекбокс (✓) справа от эффекта. Он должен включиться/выключиться. Галочка окрашивается в акцентный цвет.
5. **Панель свойств** — нажмите на строку эффекта (не на чекбокс). Справа должна появиться панель с заголовком и редактируемыми свойствами (слайдеры, чекбоксы и т.д.).
6. **Чекбокс Enabled** — в панели свойств первым должен быть чекбокс «Enabled», управляющий включением/выключением эффекта.
7. **Вкладка Workshop** — переключитесь на «Workshop». Должен загрузиться список пакетов из мастерской с миниатюрами и именами авторов.
8. **Пресеты** — нажмите кнопку «Presets» в нижней части списка. Сохраните пресет, затем загрузите его — все эффекты и их свойства должны восстановиться.
9. **Стили** — убедитесь, что панели имеют полупрозрачный фон с блюром, вкладки переключаются с анимацией цвета, а выбранная строка подсвечена.
