# Этап 14_07 — UI-компоненты меню спавна

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что делают эти компоненты

Это набор **переиспользуемых UI-компонентов** для построения меню спавна. Представьте себе **кубики LEGO**: каждый компонент — это маленький, самостоятельный блок с конкретной задачей. Комбинируя их вместе, можно собрать полноценный интерфейс меню.

В этом файле разобраны 6 компонентов:
1. **SpawnMenuContent** — обёртка с заголовком и телом
2. **SpawnMenuFilter** — поле поиска с фильтрацией
3. **SpawnMenuIconOptions** — переключатель размера иконок
4. **SpawnMenuPage** — страница с боковой панелью
5. **SpawnMenuPath** — отображение пути/навигации
6. **SpawnMenuToolbar** — панель инструментов

---

## 1. SpawnMenuContent — обёртка контента

### Исходный код (Razor)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    @if (Header != null)
    {
        <div class="header">@Header</div>
    }

    <div class="body menuinner">@Body</div>
</root>

@code
{

    [Parameter] public RenderFragment Header { get; set; }
    [Parameter] public RenderFragment Body { get; set; }

}
```

### Стили (SCSS)

```scss
SpawnMenuContent
{
	width: 100%;
	height: 100%;
	flex-direction: column;
	position: relative;
}

SpawnMenuContent > .header
{
	flex-direction: column;
	flex-shrink: 0;
}

SpawnMenuContent > .body
{
	position: relative;
	flex-grow: 1;
	padding: 8px;
}

SpawnMenuContent > .body .cell:hover
{
	color: white;
	sound-in: ui.hover;
}
```

### Объяснение

- Принимает два слота: `Header` (необязательный заголовок) и `Body` (основное содержимое).
- Заголовок не сжимается (`flex-shrink: 0`), а тело занимает всё оставшееся место (`flex-grow: 1`).
- При наведении на ячейки (`.cell`) внутри тела проигрывается звук `ui.hover`.

---

## 2. SpawnMenuFilter — поле фильтрации

### Исходный код (Razor)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
<TextEntry class="filter menu-input menu-filter-input" placeholder="Filter.." Value:bind=@Query HasClearButton></TextEntry>
</root>


@code
{
	public string Query { get; set; }
}
```

### Объяснение

- Содержит единственный элемент — текстовое поле (`TextEntry`).
- Свойство `Query` двусторонне привязано к значению поля (`Value:bind=@Query`).
- `HasClearButton` — добавляет кнопку «очистить» в поле ввода.
- Родительские компоненты могут читать `Query`, чтобы фильтровать отображаемые элементы.

---

## 3. SpawnMenuIconOptions — переключатель размера иконок

### Исходный код (Razor)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    <div class="menu-icon-toggle-group">
        <IconPanel Tooltip="Compact" class=@( Size == 60 ? "active" : "" ) Text="view_compact" @onclick=@( () => ChangeSize( 60 ) )></IconPanel>
        <IconPanel Tooltip="Medium" class=@( Size == 120 ? "active" : "" ) Text="view_module" @onclick=@( () => ChangeSize( 120 ) )></IconPanel>
        <IconPanel Tooltip="Large" class=@( Size == 200 ? "active" : "" ) Text="grid_view" @onclick=@( () => ChangeSize( 200 ) )></IconPanel>
    </div>
</root>


@code
{
	public int Size { get; set; } = 120;

	protected override int BuildHash() => HashCode.Combine( Size );

	void ChangeSize( int size )
	{
		Size = size;
	}
}
```

### Объяснение

- Три кнопки для выбора размера иконок: **Compact** (60px), **Medium** (120px), **Large** (200px).
- Текущий активный размер выделяется классом `"active"`.
- `BuildHash` возвращает хеш на основе `Size` — компонент перерисовывается только при изменении размера.
- По умолчанию выбран средний размер (120).

---

## 4. SpawnMenuPage — страница с боковой панелью

### Исходный код (Razor)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    @if ( Left != null )
    {
        <div class="left">@Left</div>
    }

    <div class="body">@Body</div>
</root>

@code
{

    [Parameter] public RenderFragment Left { get; set; }
    [Parameter] public RenderFragment Body { get; set; }

}
```

### Стили (SCSS)

```scss
@import "/UI/Theme.scss";

SpawnMenuPage
{
	width: 100%;
	height: 100%;
}

SpawnMenuPage > .left
{
	width: 300px;
	flex-direction: column;
	padding: 8px;
	white-space: nowrap;
	overflow: hidden;

	> div
	{
		flex-grow: 1;
		flex-direction: column;

		h1
		{
			font-family: $subtitle-font;
			padding: 0.5rem;
			color: $menu-accent;
			flex-shrink: 0;
		}

		.grow
		{
			flex-grow: 1;
		}
	}
}

SpawnMenuPage > .body
{
	position: relative;
	flex-grow: 1;
	margin: 8px;
	margin-left: 0px;
}
```

### Объяснение

- Двухколоночный макет: необязательная **левая панель** (`Left`, 300px) и **основное тело** (`Body`).
- Левая панель имеет фиксированную ширину и скрывает переполнение (`overflow: hidden`).
- Тело занимает всё оставшееся пространство.
- Заголовки (`h1`) в левой панели стилизованы акцентным цветом темы.

---

## 5. SpawnMenuPath — отображение пути

### Исходный код (Razor)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
	@Path
</root>


@code
{
	public string Path { get; set;  }

}
```

### Объяснение

- Самый простой компонент: просто отображает текстовую строку `Path`.
- Используется для показа текущего пути навигации (например, категории или папки) в меню.

---

## 6. SpawnMenuToolbar — панель инструментов

### Исходный код (Razor)

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root class="menu-header-bar">

    @if ( Left != null )
    {
        <div class="left">@Left</div>
    }

    <div class="body">@Body</div>

@if ( Right != null )
    {
        <div class="right">@Right</div>
    }

</root>

@code
{

    [Parameter] public RenderFragment Left { get; set; }
    [Parameter] public RenderFragment Body { get; set; }
    [Parameter] public RenderFragment Right { get; set; }

}
```

### Стили (SCSS)

```scss
SpawnMenuToolbar
{
	width: 100%;
	padding: 1px 1px 8px 1px;
	gap: 0.5rem;
}

.body, .left, .right
{
	gap: 0.5rem;
	align-items: center;
	justify-content: center;
}

TextEntry
{
	min-width: 256px;
}

DropDown
{
	padding: 10px;
	gap: 16px;
	align-items: center;
	justify-content: center;

	IconPanel
	{
		height: 16px;
	}
}

.body
{
	flex-grow: 1;
}
```

### Объяснение

- Трёхсекционная панель: **левая часть** (`Left`), **центральная часть** (`Body`) и **правая часть** (`Right`).
- Левая и правая части необязательны.
- Центральная часть (`Body`) растягивается на всё доступное пространство (`flex-grow: 1`).
- Все секции выровнены по центру с промежутками (`gap: 0.5rem`).
- Текстовые поля имеют минимальную ширину 256px, выпадающие списки оформлены с иконками высотой 16px.

---

## Общие паттерны

- Все компоненты наследуют `Panel` и находятся в пространстве имён `Sandbox`.
- Используется паттерн **слотов** (`RenderFragment`) — родительский компонент передаёт содержимое внутрь.
- Стили следуют единой теме (`Theme.scss`) с переменными `$menu-accent`, `$menu-color` и др.
- Компоненты **не содержат бизнес-логики** — они отвечают только за структуру и внешний вид.


---

## ➡️ Следующий шаг

Переходи к **[14.08 — Этап 14_08 — SpawnMenu](14_08_SpawnMenu.md)**.
