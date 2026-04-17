# 14.17 — Локальные пропы (LocalProps + SpawnPageLocal) 📦

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor Panels](00_26_Razor_Basics.md)

## Что мы делаем?

Добавляем в меню спавна новый раздел **Local** — для моделей, которые поставляются вместе с игрой и **не** лежат в мастерской (workshop). Раздел состоит из двух файлов:

- `LocalProps.cs` — статический список захардкоженных путей `.vmdl` с категориями (`characters`, `props`).
- `SpawnPageLocal.razor` — страница меню: поиск + сетка иконок (`VirtualGrid`) на базе этого списка.

Раздел добавляется в [`PropsPage`](14_10_Props.md) под заголовком «Local» с тремя фильтрами: All / Characters / Props.

## Зачем это нужно?

- **Базовый набор моделей без интернета**: даже без мастерской игрок всегда может заспавнить citizen-манекен, стулья, коробки, колёса.
- **Быстрый тест карты/режима**: не нужно монтировать контент — нужные модели всегда под рукой.
- **Простое расширение**: чтобы добавить новую хардкод-модель, достаточно дописать одну строку в `LocalProps.All`.

## Как это работает внутри движка?

### `LocalProps`

| Элемент | Описание |
|---|---|
| `record Entry( string Path, string Category )` | Запись: путь к `.vmdl` и категория (`characters`/`props`/…). |
| `Entry.DisplayName` | Генерирует человеческое имя: обрезает расширение (двойное — если путь вида `.vmdl_c`), делит по `_` и делает каждое слово с большой буквы. |
| `All` | `List<Entry>` — весь набор моделей. Сейчас содержит: 4 модели `characters` (mannequin, citizen, citizen_human_male/female) и ~20 моделей `citizen_props`. |

### `SpawnPageLocal.razor`

| Элемент | Описание |
|---|---|
| `Category` | Параметр — фильтр категории (`""` = все). |
| `Filter` | Текст из поисковой строки. При изменении вызывает `Rebuild()`. |
| `Entries` | Отфильтрованный `List<LocalProps.Entry>` для рендера. |
| `Rebuild()` | Берёт `LocalProps.All`, фильтрует по `Category` и по тексту (в `DisplayName` или `Path`, регистронезависимо). |
| `VirtualGrid` | Сетка иконок размером 120 (как в других вкладках спавна). Каждая иконка — `SpawnMenuIcon` с идентификатором `prop:<path>`. |

### `PropsPage` (добавление раздела)

```csharp
AddHeader( "Local" );
AddOption( "🧍", "All",        () => new SpawnPageLocal() );
AddOption( "🙎", "Characters", () => new SpawnPageLocal() { Category = "characters" } );
AddOption( "📦", "Props",      () => new SpawnPageLocal() { Category = "props" } );
```

## Создай файлы

### `Code/UI/SpawnMenu/Props/LocalProps.cs`

```csharp
/// <summary>
/// Hardcoded models that ship with the game and aren't available on the workshop.
/// Add new entries here — they'll appear automatically in the Local props section.
/// </summary>
public static class LocalProps
{
	public record Entry( string Path, string Category )
	{
		/// <summary>
		/// Derives a display name from the filename: strips the extension,
		/// replaces underscores with spaces, and title-cases each word.
		/// </summary>
		public string DisplayName
		{
			get
			{
				var file = System.IO.Path.GetFileNameWithoutExtension( Path );
				// strip a second extension for compiled paths like .vmdl_c
				file = System.IO.Path.GetFileNameWithoutExtension( file );
				var words = file.Split( '_' );
				return string.Join( " ", words.Select( w => char.ToUpperInvariant( w[0] ) + w[1..] ) );
			}
		}
	}

	public static List<Entry> All => new()
	{
		// Characters
		new( "models/citizen_mannequin/mannequin.vmdl", "characters" ),
		new( "models/citizen/citizen.vmdl", "characters" ),
		new( "models/citizen_human/citizen_human_male.vmdl", "characters" ),
		new( "models/citizen_human/citizen_human_female.vmdl", "characters" ),

		// Props (citizen_props)
		new( "models/citizen_props/beachball.vmdl", "props" ),
		new( "models/citizen_props/broom01.vmdl", "props" ),
		new( "models/citizen_props/cardboardbox01.vmdl", "props" ),
		new( "models/citizen_props/chair01.vmdl", "props" ),
		new( "models/citizen_props/chair02.vmdl", "props" ),
		new( "models/citizen_props/chair03.vmdl", "props" ),
		new( "models/citizen_props/chair04blackleather.vmdl", "props" ),
		new( "models/citizen_props/chair05bluefabric.vmdl", "props" ),
		new( "models/citizen_props/coffeemug01.vmdl", "props" ),
		new( "models/citizen_props/concreteroaddivider01.vmdl", "props" ),
		new( "models/citizen_props/crate01.vmdl", "props" ),
		new( "models/citizen_props/crowbar01.vmdl", "props" ),
		new( "models/citizen_props/gritbin01_combined.vmdl", "props" ),
		new( "models/citizen_props/newspaper01.vmdl", "props" ),
		new( "models/citizen_props/oldoven.vmdl", "props" ),
		new( "models/citizen_props/recyclingbin01.vmdl", "props" ),
		new( "models/citizen_props/roadcone01.vmdl", "props" ),
		new( "models/citizen_props/sodacan01.vmdl", "props" ),
		new( "models/citizen_props/trashbag02.vmdl", "props" ),
		new( "models/citizen_props/trashcan01.vmdl", "props" ),
		new( "models/citizen_props/trashcan02.vmdl", "props" ),
		new( "models/citizen_props/wheel01.vmdl", "props" ),
		new( "models/citizen_props/wheel02.vmdl", "props" ),
		new( "models/citizen_props/wineglass01.vmdl", "props" ),
		new( "models/citizen_props/wineglass02.vmdl", "props" ),
	};
}
```

### `Code/UI/SpawnMenu/Props/SpawnPageLocal.razor`

```razor
@using Sandbox;
@using Sandbox.UI;

@inherits Panel
@namespace Sandbox

<SpawnMenuContent>

    <Header>
        <SpawnMenuToolbar>
            <Left>
                <TextEntry Placeholder="Search..." class="filter menu-input" Value:bind=@Filter />
            </Left>
        </SpawnMenuToolbar>
    </Header>

    <Body>
        <VirtualGrid Items=@Entries ItemSize=@(120)>
            <Item Context="item">
                @if ( item is LocalProps.Entry entry )
                {
                    <SpawnMenuIcon Ident=@($"prop:{entry.Path}") Title=@entry.DisplayName />
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

    private List<LocalProps.Entry> Entries = new();

    protected override void OnParametersSet()
    {
        Rebuild();
    }

    void Rebuild()
    {
        var source = string.IsNullOrEmpty( Category )
            ? LocalProps.All
            : LocalProps.All.Where( x => x.Category == Category ).ToList();

        if ( !string.IsNullOrWhiteSpace( Filter ) )
            source = source.Where( x => x.DisplayName.Contains( Filter, StringComparison.OrdinalIgnoreCase )
                                     || x.Path.Contains( Filter, StringComparison.OrdinalIgnoreCase ) ).ToList();

        Entries = source;
        StateHasChanged();
    }
}
```

### Обнови `Code/UI/SpawnMenu/Props/PropsPage.cs`

В методе, где формируются разделы мастерской, **после** существующих `AddOption(...)` добавь:

```csharp
AddHeader( "Local" );
AddOption( "🧍", "All", () => new SpawnPageLocal() );
AddOption( "🙎", "Characters", () => new SpawnPageLocal() { Category = "characters" } );
AddOption( "📦", "Props", () => new SpawnPageLocal() { Category = "props" } );
```

## Проверка

- [ ] Все файлы компилируются
- [ ] В меню спавна есть раздел «Local» с тремя вкладками: All / Characters / Props
- [ ] Клик по иконке спавнит выбранную модель (идёт через `SpawnMenuIcon` → `prop:<path>`)
- [ ] Поиск фильтрует по отображаемому имени и пути
- [ ] Добавление новой строки в `LocalProps.All` сразу появляется в UI без правки Razor

## Смотри также

- [14.10 — Props](14_10_Props.md) — общий подход к странице «Props».
- [14.07 — UI компоненты меню](14_07_UI_Components.md) — `SpawnMenuIcon`, `VirtualGrid`.

---

## ➡️ Следующий шаг

Возвращайся к [14.10 — Props](14_10_Props.md) или [14.11 — Ents](14_11_Ents.md).
