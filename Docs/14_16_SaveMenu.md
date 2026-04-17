# Этап 14_16 — SaveMenu, CleanupPage, UsersPage, UtilitiesPage

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём **служебные страницы** спавн-меню:

1. **SaveMenu** — полноценное меню сохранений: список сейвов с миниатюрами, загрузка, удаление, создание новых сохранений.
2. **CleanupPage** — страница очистки мира (удаление своих или всех объектов).
3. **UsersPage** — управление банами (список забаненных, разбан, копирование Steam ID).
4. **UtilitiesPage** — простая страница с утилитами (пока только «Kill Me»).
5. **UtilityTab** — контейнер-вкладка, объединяющая все страницы-утилиты в единое меню с навигацией.

### Аналогия

- **SaveMenu** — как экран сохранений в любой игре: карточки с превью, кнопка «Сохранить», подтверждение загрузки/удаления.
- **UtilityTab** — как **панель управления Windows**: слева категории (Cleanup, Users, Utilities), справа — содержимое выбранной категории.

---

## Как это работает внутри движка

1. **SaveSystem** — API движка для сохранения/загрузки состояния сцены. `SaveSystem.Current.Save(path)` сериализует всю сцену в файл, `Load(path)` — восстанавливает.
2. **FileSystem.Data** — файловая система данных игрока. Файлы хранятся в пользовательской директории (не в папке игры).
3. **SaveSystem.SaveVersion** — версия формата сохранений. При несовпадении сейв помечается как «Outdated».
4. **MenuPanel** — всплывающее контекстное меню движка.
5. **StringQueryPopup** — модальное окно с текстом и кнопками (подтверждение/отмена).
6. **CleanupSystem** — система очистки мира. RPC-вызовы (`RpcCleanUpMine`, `RpcCleanUpAll`, `RpcCleanUpTarget`) отправляют команду серверу.
7. **BanSystem** — система банов. Хранит список забаненных по SteamId.
8. **UtilityPage** — базовый класс для страниц утилит. Метод `IsVisible()` контролирует видимость.
9. Атрибут `[SpawnMenuHost.SpawnMenuMode<SpawnMenuHost.HostOnly>]` на `SaveMenu` означает, что этот режим доступен **только хосту**.

---

## Файлы этого этапа

| Путь | Назначение |
|------|-----------|
| `Code/UI/SpawnMenu/SaveMenu.razor` | Меню сохранений |
| `Code/UI/SpawnMenu/SaveMenu.razor.scss` | Стили меню сохранений |
| `Code/UI/SpawnMenu/CleanupPage.razor` | Страница очистки мира |
| `Code/UI/SpawnMenu/UsersPage.razor` | Страница управления банами |
| `Code/UI/SpawnMenu/UsersPage.razor.scss` | Стили страницы банов |
| `Code/UI/SpawnMenu/UtilitiesPage.razor` | Страница утилит |
| `Code/UI/SpawnMenu/UtilityTab.razor` | Контейнер-вкладка утилит |

---

## Полный код

### SaveMenu.razor

**Путь:** `Code/UI/SpawnMenu/SaveMenu.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits Panel
@attribute [SpawnMenuHost.SpawnMenuMode<SpawnMenuHost.HostOnly>]
@attribute [Icon( "💾" )]
@attribute [Title( "Saves" )]
@attribute [Order( 150 )]

<root>
    <div class="save-window">
        <div class="save-header">💾 Saves</div>
        <div class="body">
            @if ( !SaveFiles.Any() )
            {
                <div class="empty-state">No saves yet.</div>
            }
            @foreach ( var save in SaveFiles )
            {
                var s = save;
                <div class="save-card @(!s.IsCompatible ? "outdated" : "")"
                     @onclick="@(() => ConfirmLoad( s ))"
                     @onmousedown="@((PanelEvent e) => OnCardMouseDown( s, e ))">
                    @if ( s.Thumbnail != null )
                    {
                        <Image class="save-thumb" Texture="@s.Thumbnail" />
                    }
                    <div class="save-info @(s.Thumbnail == null ? "save-info--padded" : "")">
                        <label class="save-name">@s.DisplayName</label>
                        @if ( !string.IsNullOrEmpty( s.Timestamp ) )
                        {
                            <label class="save-timestamp">@s.Timestamp</label>
                        }
                        @if ( !s.IsCompatible )
                        {
                            <label class="save-outdated-badge">Outdated</label>
                        }
                    </div>
                    <div class="save-dots">⋮</div>
                </div>
            }
        </div>
        <div class="footer">
            <TextEntry @ref="SaveNameEntry" Placeholder="Save name..." @onsubmit="@OnSave"></TextEntry>
            <button @onclick="@OnSave">💾 Save</button>
        </div>
    </div>
</root>

@code
{
    TextEntry SaveNameEntry;
    List<SaveFileEntry> SaveFiles = new();

    record SaveFileEntry( string FileName, string DisplayName, string Timestamp, Texture Thumbnail, bool IsCompatible );
    static string SavesPath => "saves";

    protected override void OnAfterTreeRender ( bool firstTime )
    {
        if ( firstTime )
        {
            RefreshSaveList();
        }
    }

    void RefreshSaveList()
    {
        SaveFiles.Clear();

        if ( !FileSystem.Data.DirectoryExists( SavesPath ) )
        {
            FileSystem.Data.CreateDirectory( SavesPath );
        }

        foreach ( var file in FileSystem.Data.FindFile( SavesPath, "*.sav" ) )
        {
            var filePath = $"{SavesPath}/{file}";
            var displayName = file;
            string timestamp = null;
            Texture thumbnail = null;

            var metadata = SaveSystem.GetFileMetadata( filePath );
            if ( metadata != null )
            {
                if ( metadata.TryGetValue( "Title", out var name ) )
                    displayName = name;
                if ( metadata.TryGetValue( "Timestamp", out var ts ) )
                    timestamp = ts;
            }

            var isCompatible = SaveSystem.GetFileSaveVersion( filePath ) == SaveSystem.SaveVersion;

            var thumbPath = $"{SavesPath}/{System.IO.Path.GetFileNameWithoutExtension( file )}.thumb.png";
            if ( FileSystem.Data.FileExists( thumbPath ) )
            {
                thumbnail = Texture.LoadFromFileSystem( thumbPath, FileSystem.Data );
            }

            SaveFiles.Add( new SaveFileEntry( file, displayName, timestamp, thumbnail, isCompatible ) );
        }

        StateHasChanged();
    }

    void OnCardMouseDown( SaveFileEntry save, PanelEvent e )
    {
        if ( e is MousePanelEvent { MouseButton: MouseButtons.Right } me )
        {
            me.StopPropagation();
            var menu = MenuPanel.Open( this );
            menu.AddOption( "download", "Load Save", () => ConfirmLoad( save ) );
            menu.AddOption( "delete", "Delete", () => ConfirmDelete( save ) );
        }
    }

    void ConfirmLoad( SaveFileEntry save )
    {
        if ( !save.IsCompatible )
        {
            _ = new StringQueryPopup
            {
                Title = "Incompatible Save",
                Prompt = $"\"{save.DisplayName}\" was created with an older version and cannot be loaded.",
                ConfirmLabel = "OK",
                ShowInput = false,
                Parent = FindPopupPanel()
            };
            return;
        }

        _ = new StringQueryPopup
        {
            Title = "Load Save",
            Prompt = $"Load \"{save.DisplayName}\"? Unsaved changes will be lost.",
            ConfirmLabel = "Load",
            ShowInput = false,
            OnConfirm = _ => LoadSave( save.FileName ),
            Parent = FindPopupPanel()
        };
    }

    void ConfirmDelete( SaveFileEntry save )
    {
        var popup = new StringQueryPopup
        {
            Title = "Delete Save",
            Prompt = $"Are you sure you want to delete \"{save.DisplayName}\"?",
            ConfirmLabel = "Delete",
            ShowInput = false,
            OnConfirm = _ => DeleteSave( save.FileName ),
            Parent = FindPopupPanel()
        };
    }
    void OnSave()
    {
        var saveName = SaveNameEntry?.Text?.Trim();

        if ( string.IsNullOrEmpty( saveName ) )
            return;

        var now = DateTime.Now;
        var timeString = now.ToString( "yyyy-MM-dd HH:mm:ss" );
        var baseName = now.ToString( "yyyy-MM-dd-HH-mm-ss" );
        var fileName = $"{SavesPath}/{baseName}.sav";

        // Capture a 256x144 (16:9) thumbnail from the scene camera
        var camera = Game.ActiveScene?.Camera;
        if ( camera.IsValid() )
        {
            var bitmap = new Bitmap( 256, 144 );
            camera.RenderToBitmap( bitmap );
            FileSystem.Data.WriteAllBytes( $"{SavesPath}/{baseName}.thumb.png", bitmap.ToPng() );
        }

        SaveSystem.Current.SetMetadata( "Timestamp", timeString );
        SaveSystem.Current.SetMetadata( "Title", saveName );
        SaveSystem.Current.Save( fileName );

        SaveNameEntry.Text = "";
        RefreshSaveList();
    }

    void LoadSave( string fileName )
    {
        _ = SaveSystem.Current.Load( $"{SavesPath}/{fileName}" );
    }

    void DeleteSave( string fileName )
    {
        FileSystem.Data.DeleteFile( $"{SavesPath}/{fileName}" );
        var thumbPath = $"{SavesPath}/{System.IO.Path.GetFileNameWithoutExtension( fileName )}.thumb.png";
        if ( FileSystem.Data.FileExists( thumbPath ) )
            FileSystem.Data.DeleteFile( thumbPath );
        RefreshSaveList();
    }
}
```

---

### SaveMenu.razor.scss

**Путь:** `Code/UI/SpawnMenu/SaveMenu.razor.scss`

```scss
@import "/UI/Theme.scss";

SaveMenu
{
    width: 100%;
    height: 100%;
    flex-direction: column;
    align-items: center;
    justify-content: flex-end;
    padding-bottom: calc( $deadzone-y + 96px + 8px );
}

.save-window
{
    background-color: $menu-panel-bg;
    backdrop-filter: blur( 8px );
    border: 1px solid $menu-surface-soft;
    border-radius: 8px;
    padding: 1rem;
    width: 500px;
    max-height: 400px;
    flex-direction: column;

    .save-header
    {
        font-size: 13px;
        font-weight: 700;
        color: rgba( white, 0.5 );
        text-transform: uppercase;
        letter-spacing: 0.08em;
        padding-bottom: 8px;
        flex-shrink: 0;
    }

    .body
    {
        flex-direction: column;
        flex-grow: 1;
        overflow-y: scroll;
        gap: 4px;
    }

    .empty-state
    {
        text-align: center;
        color: rgba( white, 0.35 );
        font-size: 12px;
        padding: 1rem 0;
    }

    .footer
    {
        flex-direction: row;
        gap: 6px;
        padding-top: 8px;
        flex-shrink: 0;

        TextEntry
        {
            flex-grow: 1;
            padding: 6px 10px;
            background-color: rgba( white, 0.05 );
            border-radius: 6px;
            font-size: 13px;
            border: 1px solid rgba( white, 0.08 );

            &:focus
            {
                border-color: $color-accent;
                background-color: rgba( white, 0.08 );
            }
        }

        button
        {
            padding: 6px 14px;
            background-color: $menu-accent-soft;
            border: 1px solid $menu-accent;
            border-radius: 6px;
            color: white;
            font-size: 13px;
            font-weight: 600;
            cursor: pointer;
            flex-shrink: 0;

            &:hover
            {
                background-color: rgba( $color-accent, 0.3 );
                sound-in: ui.hover;
            }

            &:active
            {
                background-color: $menu-accent;
            }
        }
    }
}

.save-card
{
    flex-direction: row;
    align-items: center;
    background-color: rgba( white, 0.04 );
    border-radius: 4px;
    overflow: hidden;
    cursor: pointer;
    flex-shrink: 0;
    gap: 10px;

    &:hover
    {
        background-color: rgba( white, 0.08 );
        sound-in: ui.hover;

        .save-name { color: white; }
    }

    &:active
    {
        background-color: rgba( white, 0.12 );
    }

    &.outdated
    {
        opacity: 0.5;
        cursor: default;

        &:hover
        {
            background-color: rgba( white, 0.04 );
            sound-in: none;

            .save-name { color: rgba( white, 0.8 ); }
        }
    }

    .save-outdated-badge
    {
        font-size: 10px;
        font-weight: 700;
        color: rgba( orange, 0.85 );
        text-transform: uppercase;
        letter-spacing: 0.06em;
        margin-top: 2px;
    }


    {
        width: 96px;
        height: 54px;
        flex-shrink: 0;
        background-size: cover;
        background-position: center;
        background-color: rgba( white, 0.06 );
    }

    .save-info
    {
        flex-direction: column;
        gap: 2px;
        flex-grow: 1;
        padding: 8px 8px 8px 10px;

        &--padded
        {
            padding: 8px 10px;
        }
    }

    .save-name
    {
        font-size: 13px;
        color: rgba( white, 0.8 );
        font-weight: 600;
    }

    .save-timestamp
    {
        font-size: 11px;
        color: rgba( white, 0.35 );
        font-weight: 400;
    }

    .save-dots
    {
        font-size: 18px;
        color: rgba( white, 0.25 );
        padding: 0 10px;
        flex-shrink: 0;
        align-items: center;
        justify-content: center;
        letter-spacing: -2px;
    }

    &:hover .save-dots
    {
        color: rgba( white, 0.6 );
    }
}
```

---

### CleanupPage.razor

**Путь:** `Code/UI/SpawnMenu/CleanupPage.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits UtilityPage
@namespace Sandbox
@attribute [Icon( "🧹" )]
@attribute [Title( "Cleanup" )]
@attribute [Group( "World" )]
@attribute [Order( 0 )]

<root class="page" style="flex-direction: column;">
    <div class="control-row" @onclick=@(() =>CleanUpMine() )>
        <label class="left">🧼 Clean Up Mine</label>
    </div>

    @if ( Connection.Local.IsHost )
    {
        <div class="control-row" @onclick=@(() =>CleanUpAll() )>
            <label class="left">🧹 Clean Up All</label>
        </div>

        <div class="section-header">Clean Up Player</div>

        @foreach ( var connection in Connection.All )
        {
            var c = connection;
            <div class="control-row" @onclick=@(() => CleanUpPlayer( c ))>
                <label class="left">🗑️ @c.DisplayName</label>
            </div>
        }
    }
</root>

@code
{
    void CleanUpMine() => CleanupSystem.RpcCleanUpMine();
    void CleanUpAll() => CleanupSystem.RpcCleanUpAll();
    void CleanUpPlayer( Connection c ) => CleanupSystem.RpcCleanUpTarget( c );
}
```

---

### UsersPage.razor

**Путь:** `Code/UI/SpawnMenu/UsersPage.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits UtilityPage
@namespace Sandbox
@attribute [Icon( "👥" )]
@attribute [Title( "Users" )]
@attribute [Group( "World" )]
@attribute [Order( 1 )]

<root class="page" style="flex-direction: column;">

    <div class="section-header">Ban List</div>

    @{
        var bans = BanSystem.Current?.GetBannedList();
    }

    @if ( bans is null || bans.Count == 0 )
    {
        <div class="control-row">
            <label class="left" style="opacity: 0.5;">No one has been banned yet.</label>
        </div>
    }
    else
    {
        @foreach ( var (steamId, entry) in bans )
        {
            var sid = steamId;
            var e = entry;
            <div class="ban-entry">
                <img class="avatar" src="avatar:@sid" />
                <div class="ban-info">
                    <label class="ban-name">@e.DisplayName</label>
                    <label class="ban-reason">@e.Reason</label>
                </div>
                <div class="ban-options" @onclick=@(() => OpenBanOptions( sid, e ))>
                    <i>more_vert</i>
                </div>
            </div>
        }
    }

</root>

@code
{
    public override bool IsVisible() => Networking.IsHost;

    void OpenBanOptions( SteamId steamId, BanSystem.BanEntry entry )
    {
        var menu = MenuPanel.Open( this );

        menu.AddOption( "person_add", "Unban", () =>
        {
            BanSystem.Current?.Unban( steamId );
            StateHasChanged();
        } );

        menu.AddOption( "content_copy", "Copy Steam ID", () =>
        {
            Clipboard.SetText( steamId.ToString() );
            Notices.AddNotice( "copy_all", Color.Cyan, $"Copied {entry.DisplayName}'s Steam ID to clipboard", 5 );
        } );
    }
}
```

---

### UsersPage.razor.scss

**Путь:** `Code/UI/SpawnMenu/UsersPage.razor.scss`

```scss
@import "/UI/Theme.scss";

UsersPage .ban-entry
{
    flex-direction: row;
    align-items: center;
    padding: 6px 8px;
    border-radius: 4px;
    margin: 2px 0px;
    background-color: #53698111;

    &:hover
    {
        background-color: #53698122;
    }
}

UsersPage .ban-entry .avatar
{
    width: 36px;
    height: 36px;
    border-radius: 4px;
    flex-shrink: 0;
}

UsersPage .ban-entry .ban-info
{
    flex-direction: column;
    flex-grow: 1;
    margin-left: 10px;
    justify-content: center;
}

UsersPage .ban-entry .ban-name
{
    font-size: 13px;
    font-weight: 600;
    color: $menu-color;
}

UsersPage .ban-entry .ban-reason
{
    font-size: 11px;
    opacity: 0.55;
    color: $menu-color;
    margin-top: 2px;
}

UsersPage .ban-entry .ban-options
{
    flex-shrink: 0;
    width: 28px;
    height: 28px;
    border-radius: 4px;
    align-items: center;
    justify-content: center;
    opacity: 0.5;

    &:hover
    {
        background-color: rgba( 255, 255, 255, 0.12 );
        opacity: 1;
    }

    i
    {
        font-size: 18px;
    }
}
```

---

### UtilitiesPage.razor

**Путь:** `Code/UI/SpawnMenu/UtilitiesPage.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits UtilityPage
@namespace Sandbox
@attribute [Icon( "🔧" )]
@attribute [Title( "Utilities" )]
@attribute [Group( "Utilities" )]
@attribute [Order( 0 )]

<root class="page" style="flex-direction: column;">
    <div class="control-row" @onclick=@(() => ConsoleSystem.Run( "kill" ))>
        <label class="left">🚿 Kill Me</label>
    </div>
</root>
```

---

### UtilityTab.razor

**Путь:** `Code/UI/SpawnMenu/UtilityTab.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox
@implements IUtilityTab
@attribute [Icon("🌍")]
@attribute [Title("Utilities")]
@attribute [Order(0)]

<root class="tab">
    <div class="left">
        <VerticalMenu class="menuinner">
            <Options>
                @foreach ( var group in GetVisiblePages().GroupBy( x => x.Group ).OrderBy( x => x.Min( t => t.Order ) ) )
                {
                    @if ( !string.IsNullOrWhiteSpace( group.Key ) )
                    {
                        <h2>@group.Key</h2>
                    }

                    @foreach ( var type in group.OrderBy( x => x.Order ).ThenBy( x => x.Title ) )
                    {
                        <MenuOption Text="@type.Title" Icon="@type.Icon"
                            class=@( SelectedPageType == type ? "active" : "" )
                            @onclick="@(() => OnSelect( type ))">
                        </MenuOption>
                    }
                }
            </Options>
        </VerticalMenu>
    </div>

    <div class="body menuinner" @ref="PageContainer"></div>
</root>

@code
{
    TypeDescription SelectedPageType { get; set; }
    Panel PageContainer;
    UtilityPage ActivePage;

    IEnumerable<TypeDescription> GetVisiblePages()
    {
        foreach ( var type in Game.TypeLibrary.GetTypes<UtilityPage>() )
        {
            if ( type.IsAbstract ) continue;
            var instance = type.Create<UtilityPage>();
            if ( instance is null || !instance.IsVisible() ) continue;
            instance.Delete();
            yield return type;
        }
    }

    void OnSelect( TypeDescription type )
    {
        SelectedPageType = type;

        ActivePage?.Delete();
        ActivePage = type.Create<UtilityPage>();
        PageContainer.AddChild( ActivePage );

        StateHasChanged();
    }
}
```

---

## Разбор кода

### SaveMenu.razor — Меню сохранений

#### Атрибуты

```razor
@attribute [SpawnMenuHost.SpawnMenuMode<SpawnMenuHost.HostOnly>]
@attribute [Icon( "💾" )]
@attribute [Title( "Saves" )]
@attribute [Order( 150 )]
```
- `SpawnMenuMode<HostOnly>` — этот **режим** спавн-меню доступен **только хосту** сервера. Обычные игроки не увидят кнопку «Saves» в `SpawnMenuModeBar`.
- Это **не вкладка** внутри меню (как Tools), а **отдельный режим** (как переключение между основным меню и сохранениями).

#### Разметка

```razor
<div class="save-window">
```
Окно сохранений: заголовок, прокручиваемый список карточек, подвал с полем ввода и кнопкой.

```razor
@if ( !SaveFiles.Any() )
{
    <div class="empty-state">No saves yet.</div>
}
```
Если сохранений нет — **placeholder**.

```razor
<div class="save-card @(!s.IsCompatible ? "outdated" : "")">
```
Каждый сейв — карточка. Если версия несовместима — добавляется CSS-класс `outdated` (полупрозрачность).

```razor
<Image class="save-thumb" Texture="@s.Thumbnail" />
```
Миниатюра сейва (если есть) — 96×54 пикселей (16:9).

#### Код (code-блок)

**record SaveFileEntry** — **запись** (record) с полями: имя файла, отображаемое имя, время, миниатюра, совместимость. Records в C# — неизменяемые объекты данных.

**RefreshSaveList()** — основной метод:
1. Очищает список.
2. Создаёт папку `saves`, если не существует.
3. Перебирает файлы `*.sav` в папке.
4. Для каждого файла читает **метаданные** (`SaveSystem.GetFileMetadata`) — название и время.
5. Проверяет **совместимость** по версии формата.
6. Ищет **миниатюру** (файл `.thumb.png` рядом с `.sav`).
7. Добавляет в список `SaveFiles`.

**OnSave()** — сохранение:
1. Берёт имя из текстового поля.
2. Генерирует имя файла по текущей дате/времени.
3. **Делает скриншот** через `camera.RenderToBitmap(bitmap)` — 256×144 пикселей.
4. Сохраняет миниатюру как PNG.
5. Устанавливает метаданные (время и название).
6. Вызывает `SaveSystem.Current.Save(fileName)`.
7. Обновляет список.

**ConfirmLoad()** — загрузка с подтверждением:
- Если сейв **несовместим** — модальное окно с ошибкой.
- Если совместим — модальное окно «Load? Unsaved changes will be lost.»

**DeleteSave()** — удаляет файл `.sav` и миниатюру `.thumb.png`.

**OnCardMouseDown()** — **правый клик** открывает контекстное меню с опциями Load и Delete.

---

### SaveMenu.razor.scss — Стили сохранений

```scss
SaveMenu
{
    justify-content: flex-end;
    padding-bottom: calc( $deadzone-y + 96px + 8px );
}
```
Окно сохранений **прижато к низу** экрана с учётом «мёртвой зоны» (`$deadzone-y`).

```scss
.save-window
{
    background-color: $menu-panel-bg;
    backdrop-filter: blur( 8px );
```
**Размытие фона** — современный визуальный эффект.

```scss
.save-card.outdated
{
    opacity: 0.5;
    cursor: default;
}
```
Устаревшие сейвы — полупрозрачные и некликабельные.

```scss
sound-in: ui.hover;
```
**Звук** при наведении — встроенная система звуков UI движка.

---

### CleanupPage.razor — Очистка мира

```razor
@inherits UtilityPage
@attribute [Icon( "🧹" )]
@attribute [Title( "Cleanup" )]
@attribute [Group( "World" )]
@attribute [Order( 0 )]
```
Наследуется от `UtilityPage` — базовый класс для страниц утилит. Группа «World», порядок 0.

```razor
<div class="control-row" @onclick=@(() => CleanUpMine())>
    <label class="left">🧼 Clean Up Mine</label>
</div>
```
Кнопка «Очистить мои объекты». Доступна **всем игрокам**.

```razor
@if ( Connection.Local.IsHost )
```
Блок **только для хоста**: кнопка «Clean Up All» и список игроков для точечной очистки.

```csharp
void CleanUpMine() => CleanupSystem.RpcCleanUpMine();
```
**RPC-вызов** — Remote Procedure Call. Клиент отправляет запрос серверу, сервер удаляет объекты и синхронизирует состояние.

---

### UsersPage.razor — Управление банами

```csharp
public override bool IsVisible() => Networking.IsHost;
```
Страница **видна только хосту**. Метод `IsVisible()` переопределён из `UtilityPage`.

```razor
var bans = BanSystem.Current?.GetBannedList();
```
Получаем список банов. `?.` — безопасный доступ (если `BanSystem.Current` — null, вернёт null).

```razor
<img class="avatar" src="avatar:@sid" />
```
`avatar:` — специальная схема URL движка: загружает аватар Steam-пользователя по SteamId.

```csharp
void OpenBanOptions( SteamId steamId, BanSystem.BanEntry entry )
```
Открывает контекстное меню:
- **Unban** — разбан через `BanSystem.Current?.Unban(steamId)`, затем обновление UI.
- **Copy Steam ID** — копирует ID в буфер обмена и показывает уведомление через `Notices.AddNotice`.

---

### UtilitiesPage.razor — Простые утилиты

```razor
<div class="control-row" @onclick=@(() => ConsoleSystem.Run( "kill" ))>
    <label class="left">🚿 Kill Me</label>
</div>
```
Единственная утилита — **самоубийство** через консольную команду `kill`. `ConsoleSystem.Run` выполняет команду движка.

---

### UtilityTab.razor — Контейнер утилит

Это **вкладка-контейнер**, объединяющая все `UtilityPage` в единый интерфейс.

```razor
@implements IUtilityTab
@attribute [Icon("🌍")]
@attribute [Title("Utilities")]
@attribute [Order(0)]
```

#### Левая панель

```csharp
IEnumerable<TypeDescription> GetVisiblePages()
```
Перебирает **все классы**, наследующие `UtilityPage`:
1. Пропускает абстрактные.
2. Создаёт **временный экземпляр** (`type.Create<UtilityPage>()`).
3. Проверяет `IsVisible()`.
4. **Удаляет** временный экземпляр (`instance.Delete()`).
5. Возвращает `TypeDescription` видимых страниц.

Это нужно потому, что `IsVisible()` — **метод экземпляра** (зависит от состояния, например, является ли игрок хостом).

```razor
@foreach ( var group in GetVisiblePages().GroupBy( x => x.Group ).OrderBy( x => x.Min( t => t.Order ) ) )
```
Группировка по `Group`, сортировка по минимальному `Order` в группе.

#### Правая панель

```razor
<div class="body menuinner" @ref="PageContainer"></div>
```
`@ref="PageContainer"` — получаем **ссылку** на DOM-элемент. Через неё мы программно добавляем дочерние компоненты.

```csharp
void OnSelect( TypeDescription type )
{
    ActivePage?.Delete();
    ActivePage = type.Create<UtilityPage>();
    PageContainer.AddChild( ActivePage );
}
```
При выборе пункта:
1. Удаляем предыдущую страницу (`Delete()`).
2. Создаём новый экземпляр выбранного типа.
3. Добавляем в контейнер.

---

## Что проверить

1. Откройте спавн-меню **как хост** — в `SpawnMenuModeBar` должна быть кнопка **Saves** (💾).
2. Переключитесь в режим Saves — должно появиться **окно сохранений** внизу экрана.
3. Введите имя и нажмите Save — должен создаться сейв с **миниатюрой**.
4. Сейв должен появиться в списке с **именем, временем и превью**.
5. Кликните на сейв — должно появиться **подтверждение загрузки**.
6. Правый клик — **контекстное меню** с Load и Delete.
7. Перейдите во вкладку **Utilities** (🌍) — слева группы World и Utilities.
8. **Cleanup** — кнопки «Clean Up Mine», «Clean Up All» (только хост), список игроков.
9. **Users** (только хост) — список забаненных с аватарами, разбан, копирование Steam ID.
10. **Utilities** — кнопка «Kill Me» должна убить вашего персонажа.
11. Как **обычный игрок** (не хост): кнопка Saves не видна, страница Users не видна, в Cleanup нет «Clean Up All».


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[15.01 — Выброшенное оружие (DroppedWeapon) 🔫](15_01_DroppedWeapon.md)** — начало следующей фазы.
