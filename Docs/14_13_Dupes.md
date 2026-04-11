# Этап 14_13 — Dupes (Дубликаты)

## Что мы делаем?

Создаём **вкладку Dupes** в меню спавна. Эта вкладка позволяет игроку:

- просматривать **популярные** и **новые** дубликаты из мастерской (Workshop);
- фильтровать по **категориям** (транспорт, роботы, оружие и т.д.);
- загружать **локальные** дубликаты, сохранённые на диске;
- **сохранять** текущий дубликат кнопкой «Save Dupe» в подвале меню;
- **публиковать** локальные дубликаты в мастерскую;
- перетаскивать (drag-and-drop) иконки из мастерской.

### Аналогия

Представьте **галерею чертежей**: слева — папки с категориями, справа — сетка карточек с превью. Нажатие на карточку применяет чертёж к инструменту Duplicator, а правой кнопкой можно удалить или опубликовать.

---

## Как это работает внутри движка

1. **DupesPage** наследуется от `BaseSpawnMenu` — базовый класс, рисующий левое меню с опциями. Метод `Rebuild()` добавляет заголовки и пункты через `AddHeader` / `AddOption`.
2. При выборе пункта создаётся либо `DupesWorkshop` (компонент, загружающий данные из облака), либо `DupesLocal` (читает файлы с диска).
3. `DupesWorkshop` использует `Storage.Query` — API движка для запросов к серверу мастерской. Результаты приходят порциями (`HasMoreResults` / `GetNextResults`), а `VirtualGrid` рендерит только видимые ячейки для производительности.
4. `DupesLocal` получает список из `Storage.GetAll("dupe")` — локальное хранилище движка.
5. `DupesFooter` проверяет, активен ли инструмент Duplicator и есть ли скопированный JSON (`CopiedJson`). Если да — кнопка «Save Dupe» активна.
6. `LocalDupeIcon` и `WorkshopIcon` — Razor-компоненты для отрисовки карточек. Оба поддерживают правый клик с контекстным меню.
7. SCSS-файлы задают визуал: скруглённые углы, полупрозрачный фон для подписей, акцентную рамку при наведении.

---

## Файлы этого этапа

| Путь | Назначение |
|------|-----------|
| `Code/UI/SpawnMenu/Dupes/DupesPage.cs` | Страница-контейнер вкладки Dupes |
| `Code/UI/SpawnMenu/Dupes/DupesLocal.razor` | Сетка локальных дубликатов |
| `Code/UI/SpawnMenu/Dupes/DupesLocal.razor.scss` | Стили для DupesLocal (пустой) |
| `Code/UI/SpawnMenu/Dupes/DupesWorkshop.razor` | Сетка дубликатов из мастерской |
| `Code/UI/SpawnMenu/Dupes/DupesWorkshop.razor.scss` | Стили для DupesWorkshop (пустой) |
| `Code/UI/SpawnMenu/Dupes/DupesFooter.razor` | Razor-разметка подвала (кнопка Save) |
| `Code/UI/SpawnMenu/Dupes/DupesFooter.razor.cs` | Логика подвала (проверка состояния) |
| `Code/UI/SpawnMenu/Dupes/DupesFooter.razor.scss` | Стили подвала |
| `Code/UI/SpawnMenu/Dupes/LocalDupeIcon.razor` | Карточка локального дубликата |
| `Code/UI/SpawnMenu/Dupes/LocalDupeIcon.razor.scss` | Стили карточки локального дубликата |
| `Code/UI/SpawnMenu/Dupes/WorkshopIcon.razor` | Карточка дубликата из мастерской |
| `Code/UI/SpawnMenu/Dupes/WorkshopIcon.razor.scss` | Стили карточки мастерской |

---

## Полный код

### DupesPage.cs

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesPage.cs`

```csharp
using Sandbox.UI;

/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Dupes" ), Order( 3000 ), Icon( "✌️" )]
public class DupesPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "Workshop" );
		AddOption( "🎖️", "Popular Dupes", () => new DupesWorkshop() { SortOrder = Storage.SortOrder.RankedByVote } );
		AddOption( "🐣", "Newest Dupes", () => new DupesWorkshop() { SortOrder = Storage.SortOrder.RankedByPublicationDate } );

		AddHeader( "Categories" );

		foreach ( var entry in TypeLibrary.GetEnumDescription( typeof( DupeCategory ) ) )
		{
			AddOption( entry.Icon, entry.Title, () => new DupesWorkshop()
			{
				SortOrder = Storage.SortOrder.RankedByVote,
				Category = entry.Name.ToString()
			} );
		}


		AddGrow();
		AddHeader( "Local" );
		AddOption( "📂", "Local Dupes", () => new DupesLocal() );
	}

	protected override void OnMenuFooter( Panel footer )
	{
		footer.AddChild<DupesFooter>();
	}
}

public enum DupeCategory
{
	[Icon( "🚗" )]
	Vehicle,
	[Icon( "🤖" )]
	Robot,
	[Icon( "✈️" )]
	Plane,
	[Icon( "🕺🏼" )]
	Pose,
	[Icon( "🏹" )]
	Weapon,
	[Icon( "🖼️" )]
	Art,
	[Icon( "🏠" )]
	Scene,
	[Icon( "🎳" )]
	Game,
	[Icon( "🛸" )]
	Spaceship,
	[Icon( "🎰" )]
	Machine,
	[Icon( "🧸" )]
	Toys,
	[Icon( "🪤" )]
	Trap,
	[Icon( "⛵" )]
	Boat,
	[Icon( "📂" )]
	Other
}

public enum DupeMovement
{
	Static,
	Wheeled,
	Flying,
	Walking,
	Water,
	Tracked
}
```

---

### DupesLocal.razor

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesLocal.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@inherits Panel
@namespace Sandbox

<SpawnMenuContent>

    <Body>
        <VirtualGrid Items=@( GetData() ) ItemSize=@ItemSize>
            <Item Context="item">

				@if (item is Storage.Entry content)
                {
					<LocalDupeIcon Content="@content" @onclick="@(() => SwitchToDupe(content))"></LocalDupeIcon>
                }

            </Item>
        </VirtualGrid>
    </Body>

</SpawnMenuContent>

@code
{
    public static int ItemSize { get; set; } = 120;

	protected override int BuildHash() => HashCode.Combine(ItemSize);

    protected override async Task OnParametersSetAsync()
    {

    }

    void SwitchToDupe( Storage.Entry content )
    {
		var localPlayer = Player.FindLocalPlayer();
		if ( localPlayer == null ) return;

		var inventory = localPlayer.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		inventory.SetToolMode( "Duplicator" );

		var toolmode = localPlayer.GetComponentInChildren<Duplicator>();

		if ( toolmode is null )
		{
			// we are a client and didn't switch tools in time, we need to wait for the server to tell us we have the tool
			// Some kind of async wait on an rpc would work, probably.
			Log.Warning( "I thought this might happen. We are a client and didn't switch tools in time." );
			return;
		}

		var json = content.Files.ReadAllText( "/dupe.json" );
		toolmode.Load( json );
    }

    IEnumerable<object> GetData()
    {
		return Storage.GetAll( "dupe" ).OrderByDescending( x => x.Created );
    }

}
```

---

### DupesLocal.razor.scss

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesLocal.razor.scss`

```scss
// Файл пуст — стили наследуются от общей темы SpawnMenu.
```

---

### DupesWorkshop.razor

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesWorkshop.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@inherits Panel
@namespace Sandbox

<SpawnMenuContent>

    <Body>
		
        <VirtualGrid Items=@Items ItemSize=@(200) OnLastCell="@(() => { _ = QueryNext(); })">
            <Item Context="context">

				@if (context is Storage.QueryItem item)
                {
					<WorkshopIcon Item="@item" @onclick=@( () => _ = Duplicator.FromWorkshop( item ) )></WorkshopIcon>
                }

            </Item>
        </VirtualGrid>
    </Body>

</SpawnMenuContent>

@code
{
    public Storage.SortOrder SortOrder { get; set; } = Storage.SortOrder.RankedByVote;
    public string Category { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        Items.Clear();
        await QueryNext();
    }

    List<Storage.QueryItem> Items = new();
    Storage.QueryResult LastResult;

    async Task QueryNext()
    {
        if ( LastResult != null )
        {
            // No more items to load
            if (!LastResult.HasMoreResults())
                return;

            LastResult = await LastResult.GetNextResults();
            if (LastResult.Items == null) return;

            Items.AddRange(LastResult.Items);
            StateHasChanged();
            return;
        }

		var query = new Storage.Query();
		query.KeyValues["package"] = "facepunch.sandbox";
		query.KeyValues["type"] = "dupe";
		query.SortOrder = SortOrder;

        if (Category != null) query.KeyValues["category"] = Category;

		LastResult = await query.Run();
		if ( LastResult.Items == null ) return;

		Items.AddRange( LastResult.Items );
		StateHasChanged();
	}
}
```

---

### DupesWorkshop.razor.scss

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesWorkshop.razor.scss`

```scss
// Файл пуст — стили наследуются от общей темы SpawnMenu.
```

---

### DupesFooter.razor

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesFooter.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@inherits Panel
@namespace Sandbox

<root>

<Button class="menu-action primary wide" Icon="💾" Text="Save Dupe" Disabled=@( !CanSaveDupe() ) @onclick=@MakeSave></Button>

</root>
```

---

### DupesFooter.razor.cs

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesFooter.razor.cs`

```csharp
using Sandbox.UI;
namespace Sandbox;

public partial class DupesFooter : Panel
{
	protected override int BuildHash() => HashCode.Combine( CanSaveDupe() );

	bool CanSaveDupe()
	{
		var mode = Player.FindLocalToolMode<Duplicator>();
		if ( mode is null ) return false;

		// toolgun isn't out, not in duplicator mode
		if ( !mode.Active ) return false;

		if ( string.IsNullOrWhiteSpace( mode.CopiedJson ) ) return false;

		return true;
	}

	void MakeSave()
	{
		var mode = Player.FindLocalToolMode<Duplicator>();
		if ( mode is null ) return;

		mode.Save();
	}
}
```

---

### DupesFooter.razor.scss

**Путь:** `Code/UI/SpawnMenu/Dupes/DupesFooter.razor.scss`

```scss
DupesFooter
{
    width: 100%;
    flex-shrink: 0;
    padding: 8px;
}

.menu-action
{
    padding: 8px 16px;
    gap: 1rem;
}
```

---

### LocalDupeIcon.razor

**Путь:** `Code/UI/SpawnMenu/Dupes/LocalDupeIcon.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@inherits Panel
@namespace Sandbox

<root>
    <div class="name">@(Content.GetMeta<string>( "name" ) ?? "Dupe")</div>
</root>

@code
{
    public Storage.Entry Content;

    protected override void OnParametersSet()
    {
        Style.SetBackgroundImage( Content.Thumbnail );
    }

    protected override void OnMouseDown( MousePanelEvent e )
    {
        if ( e.MouseButton == MouseButtons.Right )
        {
            var menu = MenuPanel.Open( this );

            menu.AddOption( "delete", "Delete", () => Content.Delete() );
            menu.AddOption( "publish", "Publish", Publish );

            SpawnlistData.PopulateContextMenu( menu, new SpawnlistItem
            {
                Ident = SpawnlistItem.MakeIdent( "dupe", Content.Id.ToString(), "local" ),
                Title = Content.GetMeta<string>( "name" ) ?? "Dupe",
                Icon = null,
            } );

            return;
        }

        base.OnMouseDown( e );
    }

    void Publish()
    {
        var options = new Modals.WorkshopPublishOptions();
        options.Title = "My Dupe";
        options.AddCategory<DupeCategory>( "Category" );
        options.AddCategory<DupeMovement>( "Movement");

        Content.Publish(options);
    }
}
```

---

### LocalDupeIcon.razor.scss

**Путь:** `Code/UI/SpawnMenu/Dupes/LocalDupeIcon.razor.scss`

```scss
@import "/UI/Theme.scss";

LocalDupeIcon
{
    width: 100%;
    height: 100%;
    position: relative;
    overflow: hidden;
    border-radius: $hud-radius-sm;
    border: 1px solid transparent;
    background-position: center;
    background-size: contain;
    cursor: pointer;

    &:hover
    {
        border-color: rgba($color-accent, 0.2);
    }

    .name
    {
        position: absolute;
        bottom: 0px;
        left: 0px;
        background-color: #0005;
        border-radius: 10px;
        padding: 0.5rem 1rem;
        margin: 0.5rem;
        font-family: $body-font;
        font-size: 1rem;
        font-weight: 600;
        max-width: 90%;
    }
}
```

---

### WorkshopIcon.razor

**Путь:** `Code/UI/SpawnMenu/Dupes/WorkshopIcon.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@inherits Panel
@namespace Sandbox

<root>

	<div class="title">@Item.Title</div>

	<div class="author">

		<div class="avatar" style="background-image: url('@Item.Owner.Avatar');"></div>
		<div class="name">@Item.Owner.Name</div>

	</div>

</root>

@code
{
    public Storage.QueryItem Item { get; set; }

    public override bool WantsDrag => true;

    protected override void OnParametersSet()
    {
        Style.SetBackgroundImage( Item.Preview );
    }

    protected override void OnMouseDown( MousePanelEvent e )
    {
        if ( e.MouseButton == MouseButtons.Right )
        {
            var menu = MenuPanel.Open( this );

            SpawnlistData.PopulateContextMenu( menu, new SpawnlistItem
            {
                Ident = SpawnlistItem.MakeIdent( "dupe", Item.Id.ToString(), "workshop" ),
                Title = Item.Title,
                Icon = Item.Preview,
            } );

            return;
        }

        base.OnMouseDown( e );
    }

    protected override async void OnDragStart( DragEvent e )
    {
        var data = new DragData
        {
            Type = "dupe",
            Icon = Item.Preview,
            Title = Item.Title,
            Source = this
        };

        DragHandler.StartDragging( data );

		var installed = await Item.Install();
		if ( installed is null ) return;

		data.Data = installed.Files.ReadAllText( "/dupe.json" );
	}

    protected override void OnDragEnd(DragEvent e)
    {
        if ( DragHandler.IsDragging )
        {
            DragHandler.StopDragging();
        }
    }
}
```

---

### WorkshopIcon.razor.scss

**Путь:** `Code/UI/SpawnMenu/Dupes/WorkshopIcon.razor.scss`

```scss
@import "/UI/Theme.scss";

WorkshopIcon
{
    background-position: center;
    background-size: contain;
    width: 100%;
    height: 100%;
    position: relative;
    border-radius: $hud-radius-sm;
    overflow: hidden;
    border: 1px solid transparent;
    font-family: $body-font;
    font-size: 1rem;

    &:hover
    {
        border-color: rgba($color-accent, 0.2);
    }
}

.title, .author
{
    background-color: #0005;
    border-radius: 10px;
    padding: 0.5rem 1rem;
    margin: 0.5rem;
    font-weight: 600;
    max-width: 90%;
}

.title
{
    position: absolute;
    top: 0px;
    left: 0px;
}

.author
{
    position: absolute;
    bottom: 0px;
    right: 0px;
    align-items: center;
    gap: 0.5rem;

    .avatar
    {
        width: 24px;
        height: 24px;
        background-position: center;
        background-size: contain;
        border-radius: 5px;
    }
}
```

---

## Разбор кода

### DupesPage.cs — Главная страница вкладки

Это **точка входа** всей вкладки. Разберём построчно:

```csharp
[Title( "Dupes" ), Order( 3000 ), Icon( "✌️" )]
```
Три **атрибута** — метаданные класса. Движок читает их, чтобы узнать:
- `Title` — название вкладки, которое видит игрок;
- `Order` — порядок сортировки (3000 — ближе к концу);
- `Icon` — эмодзи, отображаемый рядом с названием.

```csharp
public class DupesPage : BaseSpawnMenu
```
Наследуемся от `BaseSpawnMenu` — базовый класс, который уже умеет рисовать **список слева** с заголовками и пунктами, а **справа** — контентную область. Нам нужно только заполнить список.

```csharp
protected override void Rebuild()
```
Движок вызывает `Rebuild()` каждый раз, когда нужно **перестроить меню** (при открытии, при изменении данных). Внутри мы описываем структуру:

- `AddHeader("Workshop")` — добавляет **заголовок-разделитель** в список.
- `AddOption("🎖️", "Popular Dupes", () => new DupesWorkshop() { ... })` — добавляет **пункт меню**. Третий аргумент — **фабрика**: лямбда, которая создаёт панель контента при клике. Здесь создаётся `DupesWorkshop` с сортировкой по голосам.
- `AddOption("🐣", "Newest Dupes", ...)` — аналогично, но сортировка по дате публикации.

Далее идёт **цикл по категориям**:

```csharp
foreach ( var entry in TypeLibrary.GetEnumDescription( typeof( DupeCategory ) ) )
```
`TypeLibrary.GetEnumDescription` — метод движка, который перебирает значения перечисления `DupeCategory` и возвращает **описания** (Title, Icon, Name). Для каждой категории создаётся пункт меню, который открывает `DupesWorkshop` с фильтром по категории.

```csharp
AddGrow();
```
Вставляет **растягивающийся пробел** — пункт «Local Dupes» прижмётся к низу списка.

```csharp
protected override void OnMenuFooter( Panel footer )
{
    footer.AddChild<DupesFooter>();
}
```
`BaseSpawnMenu` предоставляет **подвал** — область внизу меню. Мы добавляем в неё компонент `DupesFooter`.

**Перечисление DupeCategory** задаёт все категории. У каждого значения есть атрибут `[Icon]` с эмодзи — именно его `GetEnumDescription` возвращает в поле `Icon`.

**Перечисление DupeMovement** задаёт тип движения (статичный, колёсный, летающий и т.д.) — используется при публикации в мастерскую.

---

### DupesLocal.razor — Локальные дубликаты

```razor
@inherits Panel
```
Razor-компонент, наследующий `Panel` — базовый элемент UI движка.

```razor
<SpawnMenuContent>
    <Body>
        <VirtualGrid Items=@( GetData() ) ItemSize=@ItemSize>
```
`SpawnMenuContent` — обёртка с разметкой (заголовок + тело). `VirtualGrid` — **виртуализированная сетка**: рендерит только те ячейки, которые видны на экране. Это критично для производительности, когда элементов сотни.

- `Items` — коллекция данных для отображения;
- `ItemSize` — размер ячейки в пикселях (по умолчанию 120).

```razor
@if (item is Storage.Entry content)
{
    <LocalDupeIcon Content="@content" @onclick="@(() => SwitchToDupe(content))"></LocalDupeIcon>
}
```
Для каждого элемента сетки проверяем тип и рендерим `LocalDupeIcon`. При клике вызывается `SwitchToDupe`.

В **code-блоке**:

```csharp
void SwitchToDupe( Storage.Entry content )
```
Эта функция:
1. Находит **локального игрока** (`Player.FindLocalPlayer()`).
2. Получает компонент **инвентаря** (`PlayerInventory`).
3. Переключает инструмент на **Duplicator** (`SetToolMode("Duplicator")`).
4. Находит компонент `Duplicator` у игрока.
5. Читает **JSON-файл** дубликата из хранилища (`content.Files.ReadAllText("/dupe.json")`).
6. Загружает его в инструмент (`toolmode.Load(json)`).

```csharp
IEnumerable<object> GetData()
{
    return Storage.GetAll( "dupe" ).OrderByDescending( x => x.Created );
}
```
Получает **все локальные записи** типа "dupe" и сортирует по дате создания (новые первые).

---

### DupesWorkshop.razor — Дубликаты из мастерской

Принцип похож на `DupesLocal`, но данные загружаются **из облака**.

```csharp
public Storage.SortOrder SortOrder { get; set; } = Storage.SortOrder.RankedByVote;
public string Category { get; set; }
```
Свойства задаются при создании компонента из `DupesPage`.

```csharp
protected override async Task OnParametersSetAsync()
{
    Items.Clear();
    await QueryNext();
}
```
При установке параметров очищаем список и загружаем первую порцию.

```csharp
async Task QueryNext()
```
Метод **пагинации**:
- Если `LastResult` уже есть — проверяем `HasMoreResults()` и загружаем следующую страницу.
- Если нет — создаём `Storage.Query` с ключами `package`, `type`, порядком сортировки и (опционально) категорией, затем запускаем `query.Run()`.

```razor
<VirtualGrid Items=@Items ItemSize=@(200) OnLastCell="@(() => { _ = QueryNext(); })">
```
`OnLastCell` — **callback**, вызываемый когда пользователь доскроллил до конца. Это и есть **бесконечная прокрутка**.

---

### DupesFooter — Кнопка сохранения

**Razor-файл** (`DupesFooter.razor`) содержит только одну кнопку:

```razor
<Button class="menu-action primary wide" Icon="💾" Text="Save Dupe" Disabled=@( !CanSaveDupe() ) @onclick=@MakeSave></Button>
```
Кнопка **отключена** (`Disabled`), если `CanSaveDupe()` возвращает `false`.

**Code-behind** (`DupesFooter.razor.cs`):

```csharp
bool CanSaveDupe()
```
Проверяет три условия:
1. Есть ли у игрока **инструмент Duplicator** (`Player.FindLocalToolMode<Duplicator>()`);
2. **Активен** ли он (`mode.Active`);
3. Есть ли **скопированные данные** (`mode.CopiedJson` не пустой).

```csharp
void MakeSave()
```
Просто вызывает `mode.Save()` — метод инструмента, который сохраняет JSON на диск.

```csharp
protected override int BuildHash() => HashCode.Combine( CanSaveDupe() );
```
`BuildHash` — механизм движка для оптимизации перерисовки. Если хеш не изменился — компонент **не перерисовывается**.

---

### LocalDupeIcon.razor — Карточка локального дубликата

```razor
<root>
    <div class="name">@(Content.GetMeta<string>( "name" ) ?? "Dupe")</div>
</root>
```
Отображает **имя** дубликата. Если метаданные отсутствуют — показывает "Dupe".

```csharp
protected override void OnParametersSet()
{
    Style.SetBackgroundImage( Content.Thumbnail );
}
```
Устанавливает **миниатюру** как фоновое изображение карточки.

```csharp
protected override void OnMouseDown( MousePanelEvent e )
```
При **правом клике** открывается контекстное меню с опциями:
- **Delete** — удаляет запись из локального хранилища;
- **Publish** — открывает окно публикации в мастерскую;
- Дополнительные опции из `SpawnlistData.PopulateContextMenu` (добавление в спаунлисты).

```csharp
void Publish()
```
Создаёт `WorkshopPublishOptions` с двумя категориями — `DupeCategory` и `DupeMovement` — и вызывает `Content.Publish(options)`.

---

### WorkshopIcon.razor — Карточка мастерской

```razor
<div class="title">@Item.Title</div>
<div class="author">
    <div class="avatar" style="background-image: url('@Item.Owner.Avatar');"></div>
    <div class="name">@Item.Owner.Name</div>
</div>
```
Карточка показывает **название** вверху и **автора с аватаром** внизу.

```csharp
public override bool WantsDrag => true;
```
Включает поддержку **drag-and-drop**.

```csharp
protected override async void OnDragStart( DragEvent e )
```
При начале перетаскивания:
1. Создаётся объект `DragData` с типом, иконкой, названием.
2. Запускается `DragHandler.StartDragging`.
3. **Асинхронно** скачивается дубликат (`Item.Install()`).
4. JSON загружается в `data.Data`.

Таким образом пользователь может **перетащить** дубликат из мастерской на панель спаунлиста.

---

### SCSS-файлы — Стили

**LocalDupeIcon.razor.scss**: карточка занимает 100% ячейки, имя — на полупрозрачном фоне (`#0005`) внизу слева. При наведении появляется акцентная рамка.

**WorkshopIcon.razor.scss**: аналогичная структура, но название — вверху слева, а автор — внизу справа. Аватар — квадрат 24×24 со скруглёнными углами.

**DupesFooter.razor.scss**: подвал занимает всю ширину, кнопка имеет внутренние отступы и промежуток (`gap`).

---

## Что проверить

1. Откройте меню спавна — вкладка **Dupes** должна появиться с иконкой ✌️.
2. В списке слева — разделы **Workshop**, **Categories**, **Local**.
3. Кликните **Popular Dupes** — справа должна появиться сетка карточек из мастерской с названиями и авторами.
4. Прокрутите вниз — должна работать **бесконечная подгрузка** (новые карточки появляются).
5. Выберите любую **категорию** (Vehicle, Robot и т.д.) — сетка должна отфильтроваться.
6. Кликните **Local Dupes** — должны отобразиться локально сохранённые дубликаты (или пустая сетка).
7. Сохраните дубликат через инструмент Duplicator — он должен появиться в списке Local Dupes.
8. Правый клик на локальном дубликате — контекстное меню с Delete и Publish.
9. Кнопка **Save Dupe** в подвале — активна только при активном Duplicator с данными.
10. Попробуйте **перетащить** карточку из мастерской — должен работать drag-and-drop.
