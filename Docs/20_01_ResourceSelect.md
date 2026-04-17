# 20_01 — Элементы управления: Выбор ресурса (ResourceSelect) 🎯

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.29 — GameResource](00_29_GameResource.md)
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём систему из четырёх файлов, которая позволяет пользователю выбирать ресурсы (модели, материалы, звуки и т.д.) прямо из инспектора. Это кнопка-контрол, которая показывает превью и название выбранного ресурса, а при клике открывает всплывающее окно с виртуальной сеткой всех доступных ресурсов нужного типа. Если включены пакеты — можно подгружать ресурсы из облака.

Система состоит из:
1. **ResourceSelectAttribute** — атрибут для пометки свойства типа `string` как «выбор ресурса».
2. **ResourceSelectControl** — Razor-компонент контрола в инспекторе (кнопка с превью).
3. **ResourceSelectPopup** — всплывающее окно со списком ресурсов.
4. **resource-picker.scss** — общие SCSS-миксины для стилизации подобных контролов.

## Как это работает внутри движка

- `ResourceSelectAttribute` — простой C#-атрибут с двумя свойствами: `Extension` (расширение файла, например `"vmdl"`) и `AllowPackages` (разрешить подгрузку из облака). Его вешают на свойство типа `string` в компоненте.
- `ResourceSelectControl` наследуется от `BaseControl` и помечен `[CustomEditor(typeof(string), WithAllAttributes = [typeof(ResourceSelectAttribute)])]`. Это значит: движок автоматически использует этот контрол для любого `string`-свойства, у которого есть `[ResourceSelect]`.
- При `Rebuild()` контрол определяет тип ресурса по расширению из атрибута (`AssetTypeAttribute.FindTypeByExtension`), затем вызывает `OnValueChanged()` для загрузки текущего значения.
- `OnValueChanged()` получает путь из `Property.As.String`, ищет ресурс через `ResourceLibrary.Get<GameResource>()` и обновляет заголовок. Если ресурс реализует `IDefinitionResource`, берётся `Title` из него.
- По клику (`OnClick`) создаётся `ResourceSelectPopup` с нужным расширением и текущим значением.
- `ResourceSelectPopup` наследуется от `BasePopup`. В `OnParametersSet()` собирает все локальные ресурсы через `ResourceLibrary.GetAll<Resource>()` и фильтрует по расширению. Если `AllowPackages = true`, асинхронно подгружает пакеты из облака через `Package.FindAsync()`.
- Для отображения используется `VirtualGrid` — виртуализированная сетка с размером ячейки 180×200. Каждый элемент — это либо `Resource`, либо `Package`.
- Кнопки сортировки (`PackageSortMode`) позволяют менять порядок облачных пакетов (Popular и т.д.).
- Клик по элементу вызывает `OnSelectedFile`, а клик по фону (backdrop) закрывает попап через `Delete()`.
- `resource-picker.scss` определяет два миксина: `resource-picker-base` (общая горизонтальная раскладка, стили наведения) и `resource-picker-control` (добавляет превью и текст). Эти миксины переиспользуются в `GameResourceControl` и `ClientInputControl`.

## Путь к файлам

```
Code/UI/Controls/ResourceSelectAttribute.cs
Code/UI/Controls/ResourceSelectControl.razor
Code/UI/Controls/ResourceSelectControl.razor.scss
Code/UI/Controls/ResourceSelectPopup.razor
Code/UI/Controls/ResourceSelectPopup.razor.scss
Code/UI/Controls/resource-picker.scss
```

## Полный код

### `ResourceSelectAttribute.cs`

```csharp
namespace Sandbox.UI;


public sealed class ResourceSelectAttribute : System.Attribute
{
	public string Extension { get; set; }
	public bool AllowPackages { get; set; }
}
```

### `ResourceSelectControl.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits BaseControl
@attribute [CustomEditor(typeof(string), WithAllAttributes = [typeof(ResourceSelectAttribute)])]



<root>
    <div class="preview">
        @if ( ResourcePath != null )
        {
            <div class="thumb" style="background-image: url( thumb:@ResourcePath )"></div>
        }
        else if ( ResourceAttribute != null )
        {
            <div class="ext-badge">@ResourceAttribute.Extension?.ToUpperInvariant()</div>
        }
        else
        {
            <div class="ext-badge">?</div>
        }
    </div>

    <div class="content">
        @if ( ResourceType == null )
        {
            <div class="title">Unknown Resource Type</div>
        }
        else if ( Title == null )
        {
            <div class="title empty">None Selected</div>
        }
        else
        {
            <div class="title">@Title</div>
        }

        @if ( ResourceAttribute != null )
        {
            <div class="subtitle">@ResourceAttribute.Name</div>
        }
    </div>

</root>

@code
{

    string Title;
    string ResourcePath;
    TypeDescription ResourceType;
    AssetTypeAttribute ResourceAttribute;

    public override void Rebuild()
    {
        base.Rebuild();

        if (Property.TryGetAttribute<ResourceSelectAttribute>(out var resTypeAttr))
        {
            ResourceType = AssetTypeAttribute.FindTypeByExtension(resTypeAttr.Extension);
            ResourceAttribute = ResourceType?.GetAttribute<AssetTypeAttribute>();
        }

        OnValueChanged();
    }

    public void OnValueChanged()
    {
        var path = Property.As.String;

        if (string.IsNullOrWhiteSpace(path))
        {
            Title = null;
            ResourcePath = null;
            return;
        }

        ResourcePath = path;

        var resource = ResourceLibrary.Get<GameResource>(path);
        if ( resource == null )
        {
            Title = "Unknown Resource";
            return;
        }

        Title = resource.ResourceName;

        if (resource is IDefinitionResource defRes )
        {
            Title = defRes.Title;
        }

    }

    void SelectFile( string filePath )
    {
        Property.As.String = filePath;
        OnValueChanged();
    }

    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);

        var allowPackages = false;

        // TODO: this doesn't seem to work in queries?
		// if (Property.TryGetAttribute<ResourceSelectAttribute>(out var attr))
		//     allowPackages = attr.AllowPackages;

        var popup = new ResourceSelectPopup();
        popup.Extension = ResourceAttribute?.Extension;
        popup.CurrentValue = Property.As.String;
        popup.AllowPackages = allowPackages;
        popup.Parent = FindPopupPanel();
        popup.OnSelectedFile = SelectFile;
    }
}
```

### `ResourceSelectControl.razor.scss`

```scss

@import 'resource-picker.scss';

ResourceSelectControl
{
	@include resource-picker-control;
}
```

### `ResourceSelectPopup.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits BasePopup

<root class="popup">

    <div class="inner" onclick:preventDefault=@true>
        
        <div class="popup-header">
            <h2>@Title</h2>
            @if (AllowPackages)
            {
                <div class="sort-buttons">
                    @foreach ( var mode in Enum.GetValues<PackageSortMode>() )
                    {
                        var m = mode;
                        <div class="sort-button @(SortMode == m ? "active" : "")" @onclick=@(() => SetSort(m))>@m.ToString()</div>
                    }
                </div>
            }
        </div>

        <VirtualGrid Items=@(_filteredItems) ItemSize=@(new Vector2( 180, 200 )) class="grid">
            <Item Context="item">
                @if (item is Resource res)
                {
                    bool selected = res.ResourcePath.Equals(CurrentValue, StringComparison.OrdinalIgnoreCase);
                    string itemClass = selected ? "item active" : "item";
                    string thumbUrl = $"thumb:{res.ResourcePath}";

                    <div class="@itemClass" @onclick=@(() => { SelectResource(res); })>

                        <div class="icon" style="background-image: url( @thumbUrl )"></div>

                        @if ( res is IDefinitionResource definitionResource )
                        {
                            <div class="title">@definitionResource.Title</div>
                            <div class="desc">@definitionResource.Description</div>
                        }
                        else
                        {
                            <div class="title">@res.ResourceName</div>
                            <div class="desc"></div>
                        }
                        
                    </div>
                }
                else if ( item is Package pkg )
                {
                    bool selected = pkg.FullIdent.Equals(CurrentValue, StringComparison.OrdinalIgnoreCase);
                    string itemClass = selected ? "item active" : "item";

                    <div class="@itemClass" @onclick=@(() => { SelectPackage(pkg); })>

                        <div class="icon" style="background-image: url( @pkg.Thumb )"></div>
                        <div class="title">@pkg.Title</div>
                        <div class="desc">@pkg.Org.Title</div>
                        
                    </div>
                }
            </Item>
        </VirtualGrid>

    </div>

</root>

@code
{
    public string Title { get; set; } = "Select Resource";
    public string Extension { get; set; } = "vmdl";
    public string CurrentValue { get; set; }
    public bool AllowPackages { get; set; }
    public Action<string> OnSelectedFile;

    PackageSortMode SortMode { get; set; } = PackageSortMode.Popular;

    void SetSort( PackageSortMode mode )
    {
        if ( SortMode == mode ) return;
        SortMode = mode;
        if ( AllowPackages )
            _ = LoadCloudPackages();
    }

    readonly List<Resource> _localResources = new();
    readonly List<Package> _cloudPackages = new();
    List<object> _filteredItems = new();

    protected override void OnParametersSet()
    {
        _localResources.Clear();
        _cloudPackages.Clear();

        _localResources.AddRange( ResourceLibrary.GetAll<Resource>().Where( FilterExtension ) );

        RebuildFilteredItems();

        if ( AllowPackages )
        {
            _ = LoadCloudPackages();
        }
    }

    void RebuildFilteredItems()
    {
        var items = new List<object>();
        items.AddRange( _localResources );
        items.AddRange( _cloudPackages );
        _filteredItems = items;
    }

    async Task LoadCloudPackages()
    {
        var query = $"sort:{SortMode.ToIdentifier()} type:{Extension}";

        var result = await Package.FindAsync( query );

        _cloudPackages.Clear();
        if ( result?.Packages != null )
        {
            _cloudPackages.AddRange( result.Packages );
        }

        RebuildFilteredItems();
        StateHasChanged();
    }

    bool FilterExtension(Resource res)
    {
        if (res == null) return false;
        if (string.IsNullOrEmpty(Extension)) return true;

        var ext = System.IO.Path.GetExtension(res.ResourcePath);
        if (string.IsNullOrEmpty(ext)) return false;

        return ext.TrimStart('.').Equals(Extension, StringComparison.OrdinalIgnoreCase);
    }

    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);

        if (e.Target == this)
            Delete();
    }

    void SelectResource(Resource res)
    {
        OnSelectedFile?.Invoke(res.ResourcePath);
        Delete();
    }

    void SelectPackage(Package pkg)
    {
        OnSelectedFile?.Invoke(pkg.FullIdent);
        Delete();
    }

}
```

### `ResourceSelectPopup.razor.scss`

```scss
@import "/UI/Popup.scss";

ResourceSelectPopup .inner
{
    width: 800px;
    height: 800px;

    .popup-header h2
    {
        flex-grow: 1;
    }

    .sort-buttons
    {
        flex-shrink: 0;
        flex-direction: row;
        gap: 2px;
        border-radius: 6px;
        overflow: hidden;

        .sort-button
        {
            font-size: 1.1rem;
            padding: 0.4rem 0.8rem;
            color: rgba( white, 0.5 );
            background-color: rgba( white, 0.05 );
            cursor: pointer;

            &:hover
            {
                color: white;
                background-color: rgba( white, 0.12 );
            }

            &.active
            {
                color: white;
                background-color: #08f5;
            }
        }
    }
}

ResourceSelectPopup .inner .grid
{
    width: 100%;
    flex-grow: 1;
    gap: 1rem;
    background-color: #222a;
    padding: 1rem;
    border-radius: 10px;
}

ResourceSelectPopup .inner .grid .item
{
    width: 100%;
    height: 100%;
    color: #aaa;
    pointer-events: all;
    border-radius: 10px;
    margin: 0.25rem;
    cursor: pointer;
    position: relative;
    flex-direction: column;
    padding: 1rem;
    font-size: 1rem;
    background-color: #0088ff02;

    .icon
    {
        height: 100px;
        font-size: 60px;
        overflow: hidden;
        justify-content: center;
        align-items: center;
        flex-shrink: 0;
        background-position: center;
        background-size: contain;
        background-repeat: no-repeat;
    }

    .title
    {
        font-weight: 700;
        font-size: 1.5rem;
        color: white;
        flex-shrink: 0;
    }

    .desc
    {
        overflow: hidden;
    }

    &:hover
    {
        background-color: #08f5;
    }

    &.active
    {
        background-color: #08f5;
    }
}
```

### `resource-picker.scss`

```scss

@mixin resource-picker-base
{
	flex-direction: row;
	align-items: center;
	flex-grow: 1;
	padding: 0.2rem 0.4rem;
	gap: 0.75rem;
	font-size: 1rem;
	border-radius: 8px;
	cursor: pointer;
	background-color: #0088ff22;
	border: 1px solid #0088ff55;

	&:hover
	{
		background-color: #0088ff44;
		border-color: #0088ff88;
	}

	&:active
	{
		background-color: #0088ff88;
		border-color: #08f;
	}
}

@mixin resource-picker-control
{
	@include resource-picker-base;

	.preview
	{
		width: 2.5rem;
		height: 2.5rem;
		flex-shrink: 0;
		background-color: rgba( 0, 0, 0, 0.3 );
		border-radius: 4px;
		align-items: center;
		justify-content: center;
		overflow: hidden;

		.thumb
		{
			width: 100%;
			height: 100%;
			background-size: cover;
			background-position: center center;
		}

		.ext-badge
		{
			font-size: 0.6rem;
			font-weight: 800;
			color: rgba( 255, 255, 255, 0.6 );
			letter-spacing: 0.05em;
		}
	}

	.content
	{
		flex-direction: column;
		flex-grow: 1;
		justify-content: center;
		gap: 0.1rem;

		.title
		{
			font-size: 0.95rem;
			font-weight: 600;
			color: #fff;

			&.empty
			{
				color: rgba( 255, 255, 255, 0.4 );
			}
		}

		.subtitle
		{
			font-size: 0.75rem;
			font-weight: 400;
			color: rgba( 255, 255, 255, 0.4 );
		}
	}
}
```

## Разбор кода

### ResourceSelectAttribute.cs

| Элемент | Описание |
|---------|----------|
| `ResourceSelectAttribute` | Атрибут, наследуемый от `System.Attribute`. Вешается на `string`-свойства компонентов для указания, что это не произвольная строка, а путь к ресурсу. |
| `Extension` | Расширение файла ресурса (например, `"vmdl"` для моделей, `"vmat"` для материалов). Определяет, какие ресурсы покажутся в попапе. |
| `AllowPackages` | Если `true`, в попапе дополнительно отображаются облачные пакеты из s&box Asset Party. |

### ResourceSelectControl.razor

| Элемент | Описание |
|---------|----------|
| `@inherits BaseControl` | Наследуется от базового класса контрола инспектора. |
| `[CustomEditor(...)]` | Атрибут привязки: движок будет использовать этот контрол для `string`-свойств, помеченных `[ResourceSelect]`. |
| `Rebuild()` | Вызывается при инициализации. Ищет `ResourceSelectAttribute` на свойстве, определяет тип ресурса по расширению через `AssetTypeAttribute.FindTypeByExtension()`. |
| `OnValueChanged()` | Читает текущий путь из `Property.As.String`. Если путь пустой — сбрасывает превью. Иначе загружает ресурс через `ResourceLibrary.Get<GameResource>()` и берёт название. Для `IDefinitionResource` использует `Title`. |
| `SelectFile()` | Колбэк, вызываемый попапом. Записывает новый путь в свойство и обновляет UI. |
| `OnClick()` | Создаёт `ResourceSelectPopup`, передаёт расширение, текущее значение, и колбэк `SelectFile`. `FindPopupPanel()` находит корневую панель для размещения попапа. |
| Превью-секция | Показывает миниатюру (`thumb:путь`), бейдж расширения или знак вопроса, в зависимости от состояния. |

### ResourceSelectPopup.razor

| Элемент | Описание |
|---------|----------|
| `@inherits BasePopup` | Наследуется от базового попапа, который предоставляет оверлей-фон и механику закрытия. |
| `VirtualGrid` | Виртуализированная сетка — рендерит только видимые элементы. `ItemSize` задаёт размер ячейки (180×200 пикселей). |
| `OnParametersSet()` | Жизненный цикл Razor — вызывается при установке параметров. Загружает все локальные ресурсы, фильтрует по расширению. |
| `LoadCloudPackages()` | Асинхронно запрашивает пакеты из облака через `Package.FindAsync()`. Формирует строку запроса из `SortMode` и `Extension`. После получения вызывает `StateHasChanged()` для перерисовки. |
| `FilterExtension()` | Сравнивает расширение файла ресурса с заданным, игнорируя регистр. |
| `SelectResource()` / `SelectPackage()` | Вызывают колбэк `OnSelectedFile` с путём или идентификатором пакета, затем закрывают попап. |
| `OnMouseDown()` | Клик по фону (target == this) закрывает попап. `onclick:preventDefault` на `.inner` предотвращает закрытие при клике внутри. |
| Кнопки сортировки | Перебирают значения `PackageSortMode` через `Enum.GetValues<>()`. Активная кнопка подсвечивается классом `active`. |

### resource-picker.scss

| Миксин | Описание |
|--------|----------|
| `resource-picker-base` | Базовая горизонтальная раскладка (`flex-direction: row`), общие стили фона (#0088ff22), рамки и hover/active состояния. Используется как основа для всех «выбирающих» контролов. |
| `resource-picker-control` | Расширяет `resource-picker-base`. Добавляет секцию `.preview` (квадрат 2.5rem с миниатюрой или бейджем расширения) и секцию `.content` (заголовок и подзаголовок). |

### ResourceSelectPopup.razor.scss

| Селектор | Описание |
|----------|----------|
| `.inner` | Внутренняя панель попапа, 800×800px. |
| `.sort-buttons` | Горизонтальный ряд кнопок сортировки с закруглёнными углами. |
| `.grid` | Контейнер виртуальной сетки с фоном `#222a` и паддингом. |
| `.item` | Карточка ресурса: иконка 100px, заголовок жирным шрифтом, описание. При наведении и в активном состоянии — голубая подсветка (`#08f5`). |

## Что проверить

1. Создайте компонент с `string`-свойством и пометьте его `[ResourceSelect(Extension = "vmdl")]` — в инспекторе должен появиться контрол с превью.
2. Кликните по контролу — должен открыться попап со списком всех `.vmdl`-моделей проекта.
3. Выберите модель — путь запишется в свойство, превью обновится.
4. Попробуйте расширение `"vmat"` — в попапе должны появиться материалы.
5. Кликните по фону попапа — он должен закрыться без выбора.
6. Убедитесь, что при пустом значении показывается «None Selected» с приглушённым стилем.


---

## ➡️ Следующий шаг

Переходи к **[20.02 — Элемент управления: GameResource-контрол (GameResourceControl) 🗂️](20_02_GameResourceControl.md)**.
