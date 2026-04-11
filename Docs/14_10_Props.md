# Этап 14_10 — Props

## Что это такое?

**Props** — это вкладка меню спавна, отвечающая за просмотр и размещение 3D-моделей (пропов) из мастерской и избранного.

### Аналогия

Представьте **каталог интернет-магазина**: вы выбираете категорию (Мебель, Животные, Еда), листаете товары и кликаете, чтобы разместить предмет в своём мире. Props работает точно так же — только вместо покупки вы «спавните» объект прямо в игру.

---

## Исходный код

### PropsPage.cs

```csharp
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Props" ), Order( 0 ), Icon( "📦" )]
public class PropsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		// spawnlists should do this
		// AddHeader( "You" );
		// AddOption( "⭐", "Favourites", () => new SpawnPageFavourites() );

		AddHeader( "Workshop" );
		AddOption( "🧠", "All", () => new SpawnPageCloud() );
		AddOption( "🥸", "Humans", () => new SpawnPageCloud() { Category = "human" } );
		AddOption( "🌲", "Nature", () => new SpawnPageCloud() { Category = "nature" } );
		AddOption( "🪑", "Furniture", () => new SpawnPageCloud() { Category = "furniture" } );
		AddOption( "🐵", "Animal", () => new SpawnPageCloud() { Category = "animal" } );
		AddOption( "🪠", "Prop", () => new SpawnPageCloud() { Category = "prop" } );
		AddOption( "🪀", "Toy", () => new SpawnPageCloud() { Category = "toy" } );
		AddOption( "🍦", "Food", () => new SpawnPageCloud() { Category = "food" } );
		AddOption( "🔫", "Guns", () => new SpawnPageCloud() { Category = "weapon" } );
	}
}
```

### SpawnPageCloud.razor

```razor
@using Sandbox;
@using Sandbox.UI;

@inherits Panel
@namespace Sandbox

@if ( LastResult is null )
{
    // loading
    return;
}

<SpawnMenuContent>

    <Header>
        <SpawnMenuToolbar>
            <Left>
                <TextEntry Placeholder="Search..." class="filter menu-input" Value:bind=@Filter />
            </Left>

            <Right>
                <DropDown Value:bind=@SortOrder />
            </Right>
        </SpawnMenuToolbar>
    </Header>

    <Body>
<VirtualGrid Items=@( LastResult.Packages ) ItemSize=@(120)>
    <Item Context="item">
        @if (item is Package entry)
        {
            <SpawnMenuIcon Ident=@($"prop:{entry.FullIdent}") Title="@entry.Title"></SpawnMenuIcon>
        }
    </Item>
</VirtualGrid>
    </Body>

</SpawnMenuContent>

@code
{
    public string Category { get; set; } = "";

    private string Filter
    {
        get;
        set { field = value; Rebuild(); }
    }

    public PackageSortMode SortOrder
    {
        get;
        set { field = value; Rebuild(); }
    }

    protected override void OnParametersSet()
    {
        SortOrder = PackageSortMode.Popular;
        Rebuild();
    }

    Package.FindResult LastResult;

    async void Rebuild()
    {
        var query = $"sort:{SortOrder.ToIdentifier()} type:model";
        if ( !string.IsNullOrEmpty( Filter ) ) query += $" {Filter} ";
        if ( !string.IsNullOrEmpty( Category ) ) query += $" category:{Category}";

        LastResult = await Package.FindAsync( query );
        StateHasChanged();
    }
}
```

### SpawnPageFavourites.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<SpawnMenuContent>

    <Header>
        <SpawnMenuToolbar></SpawnMenuToolbar>
    </Header>

    <Body>
        <VirtualGrid Items=@( Entries ) ItemSize=@(120)>
            <Item Context="item">
                @if (item is Entry entry)
                {
                    <SpawnMenuIcon Ident=@($"prop:{entry.Ident}")></SpawnMenuIcon>
                }
            </Item>
        </VirtualGrid>
    </Body>

</SpawnMenuContent>

@code
{
    record class Entry( string Icon, string Ident );

    private readonly List<Entry> Entries = new()
    {
        new("https://cdn.sbox.game/asset/facepunch.oildrumexplosive/thumb.png.fff8e3787c17283", "facepunch.oildrumexplosive"),
        new("https://cdn.sbox.game/asset/facepunch.watermelon/thumb.png.683db0caeab55816", "facepunch.watermelon"),
        new("https://cdn.sbox.game/asset/facepunch.toolchest/thumb.png.e22d621990091e0d", "facepunch.toolchest"),
        new("https://cdn.sbox.game/asset/facepunch.cabinet_a3/thumb.png.1bf467b6816cecf8", "facepunch.cabinet_a3"),
        new("https://cdn.sbox.game/asset/facepunch.wooden_chair_a/thumb.png.e37e2f0a1d36a772", "facepunch.wooden_chair_a"),
        new("https://cdn.sbox.game/asset/facepunch.water_drum_01/thumb.png.34b58345ab151a28", "facepunch.water_drum_01"),
        new("https://cdn.sbox.game/asset/facepunch.metalwheelbarrow/thumb.png.25663142bc944891", "facepunch.metalwheelbarrow"),
        new("https://cdn.sbox.game/asset/facepunch.hoovera/thumb.png.740d47f682e9ebb9", "facepunch.hoovera"),
        new("https://cdn.sbox.game/asset/facepunch.washingmachine/thumb.png.7aca1114cc535187", "facepunch.washingmachine"),
        new("https://cdn.sbox.game/asset/fpopium.crackhead_02/thumb.png.195fcd31c282be9f", "fpopium.crackhead_02"),
        new("https://cdn.sbox.game/asset/fish.moose/thumb.png.56b890737161bf47", "fish.moose"),
        new("https://cdn.sbox.game/asset/starblue.forklifttruck/thumb.png.d684be52bb80e4d8", "starblue.forklifttruck"),
    };

    void Spawn( string ident )
    {
        GameManager.Spawn( $"prop:{ident}" );
    }
}
```

### PackageInformationPopup.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    <div class="title" onclick=@( () => Game.Overlay.ShowPackageModal( Package.FullIdent ) )>@Package.Title</div>
    <div class="org" onclick=@( () => Game.Overlay.ShowOrganizationModal(Package.Org))>
        <img src="@Package.Org.Thumb" />
        <label>@Package.Org.Title</label>
    </div>
    <div class="meta">
        <div class="created"><i>alarm</i><span>@Package.Updated.Humanize()</span></div>
    </div>

    <div class="summary">@Package.Summary</div>
</root>

@code
{
    public Package Package { get; set; }    
}
```

### PackageInformationPopup.razor.scss

```scss
@import "/UI/Theme.scss";

PackageInformationPopup
{
	width: 300px;
	flex-direction: column;
	padding: 1rem;
	font-family: $body-font;
	font-size: 12px;

	.title
	{
		font-size: 16px;
		font-weight: 600;
		color: #fff;
		cursor: pointer;
		white-space: nowrap;
		overflow: hidden;
		opacity: 0.8;

		&:hover
		{
			opacity: 1;
		}
	}

	.summary
	{
		font-size: 12px;
		font-weight: 500;
		padding: 5px 0;
	}

	.created
	{
		opacity: 0.4;
		align-items: center;
		gap: 5px;
	}

	.meta 
	{
		gap: 1rem;
		justify-content: flex-start;
	}

	.org
	{
		gap: 0.5rem;
		align-items: center;
		cursor: pointer;
		opacity: 0.8;
		padding-bottom: 5px;

		&:hover
		{
			opacity: 1;
		}

		img
		{
			width: 20px;
			height: 20px;
		}
	}
}
```

---

## Разбор важных частей кода

### PropsPage — точка входа вкладки

- **`[Title("Props"), Order(0), Icon("📦")]`** — атрибуты задают название вкладки, порядок сортировки (0 = первая) и иконку.
- **`BaseSpawnMenu`** — базовый класс, предоставляющий методы `AddHeader()` и `AddOption()` для построения левого меню.
- **`Rebuild()`** — вызывается при инициализации; создаёт категории Workshop (All, Humans, Nature, Furniture и т.д.).
- Каждая категория создаёт `SpawnPageCloud` с нужным фильтром `Category`.

### SpawnPageCloud — облачный каталог моделей

- **`Package.FindAsync(query)`** — асинхронный запрос к API мастерской; ищет модели по типу, сортировке и категории.
- **`Filter`** — текстовое поле поиска. При изменении значения автоматически вызывается `Rebuild()` благодаря кастомному сеттеру.
- **`SortOrder`** — выпадающий список сортировки (популярные, новые и т.д.).
- **`VirtualGrid`** — виртуализированная сетка; рендерит только видимые элементы для производительности.
- **`SpawnMenuIcon`** — компонент-иконка с идентификатором `prop:{ident}`, по которому система знает, что спавнить.

### SpawnPageFavourites — избранные модели

- **`Entries`** — захардкоженный список избранных пропов с URL-ами превью и идентификаторами пакетов.
- **`VirtualGrid`** — отображает избранные модели в сетке.
- **`GameManager.Spawn($"prop:{ident}")`** — команда спавна модели по идентификатору.

### PackageInformationPopup — всплывающее окно информации

- **`Package.Title`** — название пакета (кликабельное, открывает модальное окно пакета).
- **`Package.Org`** — организация-автор (кликабельная, открывает страницу организации).
- **`Package.Updated.Humanize()`** — дата обновления в человекочитаемом формате (например, «2 часа назад»).
- **`Package.Summary`** — краткое описание пакета.

### Стилизация PackageInformationPopup

- **`width: 300px`** — фиксированная ширина попапа.
- **`opacity: 0.8` → `&:hover { opacity: 1 }`** — интерактивные элементы слегка приглушены и становятся яркими при наведении.
- **`white-space: nowrap; overflow: hidden`** — длинные названия обрезаются, не ломая вёрстку.
