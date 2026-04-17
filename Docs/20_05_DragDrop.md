# 20_05 — Система перетаскивания (Drag & Drop) 🖱️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём систему **Drag & Drop** из двух файлов:
1. **DragData** — класс-контейнер данных, описывающий, что именно перетаскивается (тип, путь, иконка, заголовок, источник).
2. **DragHandler** — Razor-компонент (PanelComponent), который управляет визуальным отображением перетаскиваемого элемента и предоставляет статический API для начала и остановки перетаскивания.

Эта система используется в спавн-меню: когда игрок хватает проп, модель или сохранённую конструкцию и тащит её в игровой мир.

## Как это работает внутри движка

- `DragData` — простой POCO-класс (Plain Old CLR Object) с шестью свойствами. `Type` определяет тип спавнера (`"prop"`, `"entity"`, `"dupe"`). `Path` — путь к ресурсу или идентификатор облачного пакета. `Icon` — URL иконки для визуала. `Source` — панель-источник, чтобы не принимать дроп на себя же. `Handled` — флаг, который целевая панель ставит в `true`, если дроп обработан (например, слот-в-слот перенос), чтобы источник не выполнял вторичные действия (например, спавн в мир).
- `DragHandler` — синглтон-компонент (`Instance`). Рендерит div с фоном-иконкой, который следует за курсором мыши.
- `StartDragging(DragData)` — статический метод. Устанавливает `Current`, сохраняет данные в `Source.UserData` (для обмена между панелями), вызывает `StateHasChanged()` для перерисовки.
- `StopDragging()` — обнуляет `Current` и перерисовывает.
- `OnUpdate()` вызывается каждый кадр. Если идёт перетаскивание — перемещает `DragVisual` к позиции мыши. `Panel.ScaleFromScreen` преобразует экранные координаты в координаты UI.
- `BuildHash()` используется системой Razor для оптимизации перерисовки — если хеш не изменился, дерево не перестраивается.
- Стили делают `DragHandler` полноэкранным невидимым оверлеем (`pointer-events: none`), а `.dragging` — маленькой карточкой 64×64px с полупрозрачным фоном, блюром и акцентной рамкой.

## Путь к файлам

```
Code/UI/DragDrop/DragData.cs
Code/UI/DragDrop/DragHandler.razor
Code/UI/DragDrop/DragHandler.razor.scss
```

## Полный код

### `DragData.cs`

```csharp
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Data being dragged. Carries enough info for both the visual and the drop action.
/// </summary>
public class DragData
{
	/// <summary>
	/// The spawner type, e.g. "prop", "entity", "dupe".
	/// </summary>
	public string Type { get; set; }

	/// <summary>
	/// The cloud ident or resource path.
	/// </summary>
	public string Path { get; set; }

	/// <summary>
	/// The icon URL to display while dragging.
	/// </summary>
	public string Icon { get; set; }

	/// <summary>
	/// Optional display title.
	/// </summary>
	public string Title { get; set; }

	/// <summary>
	/// The panel that initiated the drag, so we can ignore it as a drop target.
	/// </summary>
	public Panel Source { get; set; }

	public object Data { get; set; }

	/// <summary>
	/// Set to true when a drop target has already handled this drag (e.g. slot-to-slot swap),
	/// so the source panel's OnDragEnd can skip secondary actions like dropping to the world.
	/// </summary>
	public bool Handled { get; set; }
}
```

### `DragHandler.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

<root>

	@if ( Current is not null )
	{
		var iconStyle = string.IsNullOrEmpty( Current.Icon ) ? "" : $"background-image: url({Current.Icon})";
		<div class="dragging" style="@iconStyle" @ref="DragVisual">
			@if ( string.IsNullOrEmpty( Current.Icon ) )
			{
				<div class="drag-title">@Current.Title</div>
			}
		</div>
	}

</root>

@code
{
    public static DragHandler Instance { get; private set; }

    /// <summary>
    /// The data currently being dragged, or null.
    /// </summary>
    public static DragData Current { get; private set; }

    /// <summary>
    /// True if something is actively being dragged.
    /// </summary>
    public static bool IsDragging => Current is not null;

    Panel DragVisual { get; set; }

    protected override void OnStart()
    {
        Instance = this;
    }

    protected override void OnDestroy()
    {
        if ( Instance == this )
            Instance = null;
    }

    /// <summary>
    /// Start dragging with the given data.
    /// </summary>
    public static void StartDragging( DragData data )
    {
        Current = data;
        data.Source.UserData = data;

        Instance?.StateHasChanged();
    }

    /// <summary>
    /// Stop dragging and clear all state.
    /// </summary>
    public static void StopDragging()
    {
        if (!IsDragging) return;

		Current = null;
		Instance?.StateHasChanged();
	}

	protected override void OnUpdate()
	{
		if ( !IsDragging ) return;

		if ( DragVisual is not null )
		{
			var mouse = Mouse.Position;
			DragVisual.Style.Left = Length.Pixels( mouse.x * Panel.ScaleFromScreen - 32 );
			DragVisual.Style.Top = Length.Pixels( mouse.y * Panel.ScaleFromScreen - 32 );
		}
	}

	protected override int BuildHash() => HashCode.Combine( Current );
}
```

### `DragHandler.razor.scss`

```scss
@import "/UI/Theme.scss";

DragHandler
{
	position: absolute;
	left: 0;
	top: 0;
	width: 100%;
	height: 100%;
	z-index: 10000;
	pointer-events: none;

	.dragging
	{
		position: absolute;
		pointer-events: none;
		width: 64px;
		height: 64px;
		background-size: contain;
		background-position: center center;
		background-repeat: no-repeat;
		border-radius: 4px;
		background-color: rgba( 0, 0, 0, 0.5 );
		backdrop-filter: blur( 5px );
		border: 1px solid rgba( $color-accent, 0.4 );
		box-shadow: 0px 0px 20px rgba( $color-accent, 0.3 );
		background-tint: $color-accent;
		justify-content: flex-end;
		align-items: center;

		.drag-title
		{
			font-size: 11px;
			font-weight: 600;
			font-family: $body-font;
			color: rgba( white, 0.9 );
			text-align: center;
			text-shadow: 0 1px 4px black;
			padding: 0 4px 4px 4px;
		}
	}
}
```

## Разбор кода

### DragData.cs

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Type` | `string` | Тип спавнера. Определяет, как обработать дроп: `"prop"` — заспавнить проп, `"entity"` — сущность, `"dupe"` — дупликат. |
| `Path` | `string` | Путь к ресурсу (например, `"models/citizen/citizen.vmdl"`) или идентификатор облачного пакета (например, `"facepunch.chair"`). |
| `Icon` | `string` | URL иконки для отображения во время перетаскивания. Если пустой — показывается текстовый `Title`. |
| `Title` | `string` | Текстовый заголовок. Показывается как запасной вариант, если нет иконки. |
| `Source` | `Panel` | Ссылка на панель-источник. Нужна для: 1) записи данных в `UserData`; 2) предотвращения дропа на себя же. |
| `Data` | `object` | Произвольные дополнительные данные. Могут содержать что угодно — сериализованный дупликат, конфигурацию сущности и т.д. |
| `Handled` | `bool` | Флаг «дроп обработан». Цель дропа (например, слот инвентаря) устанавливает `true`, чтобы источник не выполнял дополнительных действий. |

### DragHandler.razor

| Элемент | Описание |
|---------|----------|
| `@inherits PanelComponent` | Наследуется от `PanelComponent` — это компонент сцены, у которого есть UI-панель. Обычно добавляется на объект камеры или HUD-объект. |
| `Instance` | Статический синглтон. Устанавливается в `OnStart()`, очищается в `OnDestroy()` (только если это текущий экземпляр). |
| `Current` | Статическое свойство — данные текущего перетаскивания. `null` означает, что ничего не перетаскивается. |
| `IsDragging` | Удобное статическое свойство-геттер: `Current is not null`. |
| `DragVisual` | Ссылка на div `.dragging`, привязанная через `@ref`. Используется для позиционирования. |
| `StartDragging()` | Устанавливает `Current`, сохраняет данные в `Source.UserData` (общее хранилище на панели, доступное другим компонентам), вызывает перерисовку. |
| `StopDragging()` | Проверяет `IsDragging`, обнуляет `Current`, перерисовывает. |
| `OnUpdate()` | Каждый кадр: если идёт перетаскивание, перемещает `DragVisual` к позиции мыши. `Mouse.Position` — экранные координаты. `Panel.ScaleFromScreen` — коэффициент масштаба UI. Смещение `-32` центрирует элемент (64/2). |
| `BuildHash()` | `HashCode.Combine(Current)` — перерисовка происходит только при изменении `Current` (начало/конец перетаскивания). |
| Разметка | Если `Current` не null, рендерит `.dragging` с фоновым изображением иконки. Если иконки нет — показывает `.drag-title` с текстом. |

### DragHandler.razor.scss

| Селектор | Описание |
|----------|----------|
| `DragHandler` | Полноэкранный абсолютный контейнер, `z-index: 10000` — поверх всего UI. `pointer-events: none` — не перехватывает клики. |
| `.dragging` | Квадрат 64×64px с `position: absolute`. `backdrop-filter: blur(5px)` — размытие фона за иконкой. Акцентная рамка и тень (`$color-accent` из темы). `background-tint` — тонировка фонового изображения в цвет акцента. |
| `.drag-title` | Текст внизу карточки (`justify-content: flex-end` на родителе). Мелкий шрифт 11px, тень текста для читаемости. |

### Пример использования

```csharp
// Начало перетаскивания (в обработчике мыши карточки спавн-меню)
var drag = new DragData
{
    Type = "prop",
    Path = "models/citizen/citizen.vmdl",
    Icon = "thumb:models/citizen/citizen.vmdl",
    Title = "Citizen",
    Source = this
};
DragHandler.StartDragging( drag );

// Окончание перетаскивания (в обработчике OnMouseUp)
if ( DragHandler.IsDragging )
{
    var data = DragHandler.Current;
    if ( !data.Handled )
    {
        // Спавним проп в мир по позиции курсора
        SpawnProp( data.Path );
    }
    DragHandler.StopDragging();
}
```

## Что проверить

1. Убедитесь, что `DragHandler` добавлен как компонент на HUD-объект сцены.
2. Начните перетаскивание из спавн-меню — должна появиться иконка 64×64, следующая за курсором.
3. Иконка должна иметь полупрозрачный фон с блюром и акцентную рамку.
4. Если у элемента нет иконки — должен отображаться текстовый заголовок.
5. Отпустите кнопку мыши — иконка должна исчезнуть.
6. Проверьте, что `DragData.Handled` корректно предотвращает двойную обработку.
