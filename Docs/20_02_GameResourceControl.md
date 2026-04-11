# 20_02 — Элемент управления: GameResource-контрол (GameResourceControl) 🗂️

## Что мы делаем?

Создаём **GameResourceControl** — Razor-компонент, который выступает кастомным редактором для свойств типа `GameResource` (и всех его наследников) в инспекторе. Этот контрол визуально показывает превью выбранного ресурса, его название и тип, а при клике открывает `ResourceSelectPopup` для выбора другого ресурса.

В отличие от `ResourceSelectControl`, который работает с `string`-свойствами (путь к ресурсу), `GameResourceControl` работает напрямую с объектами `GameResource`.

## Как это работает внутри движка

- Контрол помечен `[CustomEditor(typeof(GameResource))]` — движок автоматически подставляет его для любого свойства типа `GameResource` или его наследников.
- В `Rebuild()` определяется тип ресурса через `TypeLibrary.GetType(Property.PropertyType)`. Из `AssetTypeAttribute` берутся расширение и читаемое имя типа.
- `Extension` обрезается до 4 символов (`Truncate(4)`) для отображения в бейдже, если нет превью.
- `OnValueChanged()` получает значение через `Property.GetValue<GameResource>()`. Проверяет `IsValid()` — внутренний метод движка, который проверяет, что ресурс не был удалён и существует.
- При клике создаётся `ResourceSelectPopup`, куда передаётся расширение и текущий путь ресурса.
- В `SelectFile()` ресурс загружается через `ResourceLibrary.Get<GameResource>(filePath)` и устанавливается через `Property.SetValue()`.
- Стили подключают миксин `resource-picker-control` из `resource-picker.scss`, что даёт единообразный вид с `ResourceSelectControl`.

## Путь к файлам

```
Code/UI/Controls/GameResourceControl.razor
Code/UI/Controls/GameResourceControl.razor.scss
```

## Полный код

### `GameResourceControl.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits BaseControl
@attribute [CustomEditor(typeof(GameResource))]

<root>

    <div class="preview">
        @if ( ResourcePath != null )
        {
            <div class="thumb" style="background-image: url( thumb:@ResourcePath )"></div>
        }
        else
        {
            <div class="ext-badge">@Extension</div>
        }
    </div>

    <div class="content">
        @if ( Title == null )
        {
            <div class="title empty">None Selected</div>
        }
        else
        {
            <div class="title">@Title</div>
        }
        <div class="subtitle">@TypeName</div>
    </div>

</root>

@code
{
    string Title;
    string ResourcePath;
    string Extension;
    string TypeName;

    public override void Rebuild()
    {
        base.Rebuild();

        var typeDesc = TypeLibrary.GetType( Property.PropertyType );
        var assetType = typeDesc?.GetAttribute<AssetTypeAttribute>();
        Extension = assetType?.Extension?.ToUpperInvariant().Truncate( 4 ) ?? "RES";
        TypeName = assetType?.Name ?? Property.PropertyType?.Name ?? "Resource";

        OnValueChanged();
    }

    public void OnValueChanged()
    {
        var resource = Property.GetValue<GameResource>();

        if ( resource == null || !resource.IsValid() )
        {
            Title = null;
            ResourcePath = null;
            return;
        }

        Title = resource.ResourceName;
        ResourcePath = resource.ResourcePath;
    }

    void SelectFile( string filePath )
    {
        var resource = ResourceLibrary.Get<GameResource>( filePath );
        Property.SetValue( resource );
        OnValueChanged();
    }

    protected override void OnClick( MousePanelEvent e )
    {
        base.OnClick( e );

        var typeDesc = TypeLibrary.GetType( Property.PropertyType );
        var assetType = typeDesc?.GetAttribute<AssetTypeAttribute>();

        var popup = new ResourceSelectPopup();
        popup.Extension = assetType?.Extension;
        popup.CurrentValue = Property.GetValue<GameResource>()?.ResourcePath;
        popup.Parent = FindPopupPanel();
        popup.OnSelectedFile = SelectFile;
    }
}
```

### `GameResourceControl.razor.scss`

```scss

@import 'resource-picker.scss';


GameResourceControl
{
	@include resource-picker-control;
}
```

## Разбор кода

### GameResourceControl.razor

| Элемент | Описание |
|---------|----------|
| `@inherits BaseControl` | Базовый класс для всех контролов инспектора. Предоставляет доступ к `Property` (описание редактируемого свойства). |
| `[CustomEditor(typeof(GameResource))]` | Регистрирует контрол как редактор для типа `GameResource`. Движок выберет его автоматически для всех `GameResource`-свойств. |
| `Rebuild()` | Вызывается при первом построении контрола. Определяет тип ресурса через `TypeLibrary.GetType()` — это рефлексия движка, возвращающая `TypeDescription`. Из неё берётся `AssetTypeAttribute` с расширением и именем типа. |
| `Extension` | Расширение файла, усечённое до 4 символов и приведённое к верхнему регистру. Используется как текст бейджа, когда нет превью-изображения (например, «VMDL», «VMAT», «RES»). |
| `TypeName` | Человекочитаемое имя типа ресурса (например, «Model», «Material»). Отображается как подзаголовок в контроле. |
| `OnValueChanged()` | Получает текущий объект `GameResource` из свойства. `IsValid()` проверяет, что ресурс существует и не повреждён. Если ресурс невалидный — сбрасывает Title и ResourcePath. |
| `SelectFile(string filePath)` | Колбэк из `ResourceSelectPopup`. Загружает `GameResource` по пути через `ResourceLibrary.Get<>()` и устанавливает его как значение свойства через `Property.SetValue()`. |
| `OnClick()` | Снова определяет `AssetTypeAttribute` (нужно, т.к. `Rebuild` мог быть вызван с другим типом). Создаёт `ResourceSelectPopup` с расширением и текущим путём. |
| `.preview` → `.thumb` | Показывает миниатюру через протокол `thumb:путь`. Движок автоматически генерирует превью для известных типов ресурсов. |
| `.preview` → `.ext-badge` | Бейдж с аббревиатурой расширения, показывается когда превью недоступно. |
| `.content` → `.title.empty` | Текст «None Selected» с приглушённым цветом — указывает, что ресурс не выбран. |

### GameResourceControl.razor.scss

| Элемент | Описание |
|---------|----------|
| `@import 'resource-picker.scss'` | Подключает файл с общими миксинами стилей. |
| `@include resource-picker-control` | Применяет полный набор стилей: горизонтальную раскладку, превью-блок, текстовый блок, hover-эффекты. Это обеспечивает единообразный вид с `ResourceSelectControl`. |

### Отличие от ResourceSelectControl

| Параметр | ResourceSelectControl | GameResourceControl |
|----------|----------------------|---------------------|
| Тип свойства | `string` (путь к ресурсу) | `GameResource` (объект) |
| Определение типа | Через `ResourceSelectAttribute` на свойстве | Через `TypeLibrary.GetType(Property.PropertyType)` |
| Получение значения | `Property.As.String` | `Property.GetValue<GameResource>()` |
| Установка значения | `Property.As.String = path` | `Property.SetValue(resource)` |
| Атрибут привязки | `[CustomEditor(typeof(string), WithAllAttributes = ...)]` | `[CustomEditor(typeof(GameResource))]` |

## Что проверить

1. Создайте компонент с свойством типа `GameResource` (или наследника, например `Model`) — в инспекторе должен отобразиться контрол с бейджем расширения.
2. Кликните по контролу — должен открыться `ResourceSelectPopup` с ресурсами нужного типа.
3. Выберите ресурс — название и превью должны обновиться.
4. Убедитесь, что при `null`-значении показывается «None Selected».
5. Проверьте, что подзаголовок показывает корректное имя типа ресурса.
