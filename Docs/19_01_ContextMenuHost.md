# 19_01 — Context Menu Host (хост контекстного меню)

## Что мы делаем?

Создаём **ContextMenuHost** — корневую UI-панель, которая перехватывает все события мыши в игровом мире и направляет их в систему инспектора объектов. Это «режим осмотра» (`Inspect`), доступный из Spawn-меню: игрок наводит курсор на объекты в сцене, подсвечивает их обводкой (outline) и может кликнуть, чтобы выбрать и осмотреть объект.

## Как это работает внутри движка

1. **Razor-компонент** `ContextMenuHost` — это `Panel`, который растягивается на весь экран (`position: absolute; width: 100%; height: 100%`).
2. Атрибут `[SpawnMenuHost.SpawnMenuMode]` регистрирует его как вкладку в Spawn-меню. `[Title("Inspect")]` задаёт название, `[Icon("🔍")]` — иконку, `[Order(500)]` — порядок сортировки.
3. Внутри размещается единственный дочерний компонент `<Inspector>` — именно он управляет выборкой объектов, отрисовкой окна инспектора и т. д.
4. `ContextMenuHost` владеет двумя компонентами `HighlightOutline` (один для выбранных объектов, другой для наведённого), которые движок использует для подсветки.
5. Каждый кадр (`Tick`) хост обновляет курсор и вызывает `DrawHandles()`, который ищет сетевые объекты вблизи камеры и создаёт для них «ручки» (`ComponentHandle`).
6. Клики мыши (`OnMouseDown`, `OnMouseUp`) перенаправляются в `Inspector`, чтобы тот выделял или вызывал контекстное меню.

## Путь к файлу

```
Code/UI/ContextMenu/ContextMenuHost.razor
Code/UI/ContextMenu/ContextMenuHost.razor.scss
```

## Полный код

### `Code/UI/ContextMenu/ContextMenuHost.razor`

```razor
﻿@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox
@attribute [SpawnMenuHost.SpawnMenuMode]
@attribute [Icon("🔍")]
@attribute [Title("Inspect")]
@attribute [Order( 500 )]

<root>
	
    <div class="container">

        <Inspector @ref=Inspector></Inspector>

    </div>

</root>

@code
{
    Inspector Inspector = default;

    public HighlightOutline SelectedOutline;
    public HighlightOutline HoveredOutline;

    protected override void OnAfterTreeRender(bool firstTime)
    {
        base.OnAfterTreeRender(firstTime);

        //if (firstTime)
        {
            SelectedOutline ??= GameObject.AddComponent<HighlightOutline>();
            HoveredOutline ??= GameObject.AddComponent<HighlightOutline>();

            SelectedOutline.OverrideTargets = true;
            HoveredOutline.OverrideTargets = true;
        }
    }

    public override void Tick()
    {
        base.Tick();

        UpdateCursor();
        DrawHandles();
    }

    void UpdateCursor()
    {
        Style.Cursor = (Inspector?.Hovered.IsValid() ?? false) ? "pointer" : null;
    }

    protected override void OnMouseDown(MousePanelEvent e)
    {
        Sandbox.UI.InputFocus.Clear();

        if ( e.MouseButton == MouseButtons.Right )
        {
            Inspector?.WorldMouseRightDown( e );
            return;
        }

        Inspector?.WorldMouseDown( e );
    }

    protected override void OnMouseUp(MousePanelEvent e)
    {

        Inspector?.WorldMouseUp( e );
    }

    Dictionary<Component, Panel> _handles = [];

    void DrawHandles()
    {
		if ( Scene?.Camera is not { IsValid: true } )
			return;

        var objects = Scene.FindInPhysics(new Sphere( Scene.Camera.WorldPosition, 1000 ));
        var rootObjects = objects.Select( x => x.Network?.RootGameObject ).Where( x => x.IsValid() ).Distinct();

        foreach ( var o in rootObjects )
        {
            // find handles in this object
            foreach (var component in o.GetComponentsInChildren<Component>())
            {
                if (!ShouldCreateHandle(component))
                    continue;

                if ( _handles.TryGetValue(component, out var existingHandle) )
                {
                    if (existingHandle.IsValid())
                        continue;
                }

                var panel = new ComponentHandle( this, component, Inspector );
                _handles[component] = panel;
            }
        }
    }

    bool ShouldCreateHandle(Component component)
    {
        // TODO - needs a handle

        return false;
    }

}
```

### `Code/UI/ContextMenu/ContextMenuHost.razor.scss`

```scss
@import "/UI/Theme.scss";

ContextMenuHost
{
	position: absolute;
	width: 100%;
	height: 100%;
	color: #cdf;
	justify-content: center;
	font-family: $body-font;
	font-size: 14px;
	font-weight: 600;
	z-index: 500;

	> .container
	{
		position: absolute;
		top: 0px;
		bottom: 0px;
		right: 0px;
		left: 0px;
		margin: 0px;
		z-index: 100;
		pointer-events: none;
	}
}
```

## Разбор кода

### Razor-шаблон

| Строка | Что делает |
|--------|-----------|
| `@using Sandbox; @using Sandbox.UI;` | Импортируем основные пространства имён движка. |
| `@inherits Panel` | Компонент наследуется от `Panel` — базовый класс всех UI-элементов в s&box. |
| `@attribute [SpawnMenuHost.SpawnMenuMode]` | Регистрирует панель как режим Spawn-меню. Движок автоматически находит все классы с этим атрибутом и добавляет вкладки. |
| `@attribute [Icon("🔍")]` / `[Title("Inspect")]` / `[Order(500)]` | Метаданные вкладки — иконка, заголовок и порядок отображения. |
| `<Inspector @ref=Inspector>` | Создаём дочерний компонент `Inspector` и сохраняем ссылку на него в поле `Inspector`. |

### Блок `@code`

#### Поля

- **`Inspector Inspector`** — ссылка на дочерний компонент-инспектор. Через неё хост передаёт события мыши.
- **`SelectedOutline`** / **`HoveredOutline`** — компоненты подсветки контура. `HighlightOutline` — встроенный компонент движка, рисующий обводку вокруг рендереров.

#### `OnAfterTreeRender`

```csharp
SelectedOutline ??= GameObject.AddComponent<HighlightOutline>();
HoveredOutline ??= GameObject.AddComponent<HighlightOutline>();
```

После рендера дерева (каждый раз — закомментированная проверка `firstTime` означает, что это выполняется всегда) создаём два outline-компонента на `GameObject`, которому принадлежит панель. Флаг `OverrideTargets = true` говорит компоненту, что список целей будет задаваться вручную (а не автоматически по дочерним рендерерам).

#### `Tick`

Каждый кадр:
1. `UpdateCursor()` — если `Inspector` наводится на объект (`Hovered.IsValid()`), ставим CSS-курсор `pointer`, иначе — сбрасываем.
2. `DrawHandles()` — ищем объекты в физическом радиусе 1000 юнитов от камеры и создаём `ComponentHandle` для подходящих компонентов.

#### Обработка мыши

- **`OnMouseDown`** — сначала сбрасываем фокус ввода (`InputFocus.Clear()`). Если это правая кнопка — вызываем `WorldMouseRightDown` (контекстное меню). Иначе — `WorldMouseDown` (выборка объекта).
- **`OnMouseUp`** — передаём событие дальше.

#### `DrawHandles`

```csharp
var objects = Scene.FindInPhysics(new Sphere(Scene.Camera.WorldPosition, 1000));
```

Запрашиваем у физического движка все объекты в сфере радиусом 1000 вокруг камеры. Для каждого корневого сетевого объекта перебираем компоненты и, если `ShouldCreateHandle` возвращает `true`, создаём панельку `ComponentHandle`. Словарь `_handles` не допускает дублирования.

> **Примечание:** Сейчас `ShouldCreateHandle` всегда возвращает `false` — это место для будущей логики (TODO).

### Стили (SCSS)

- **`ContextMenuHost`** — абсолютное позиционирование на весь экран, `z-index: 500` чтобы быть поверх игры.
- **`.container`** — тоже на весь экран, но `pointer-events: none` — клики проходят сквозь контейнер и ловятся самим `ContextMenuHost`. Это позволяет хосту перехватывать все клики, но дочерние элементы (вроде окна инспектора) могут самостоятельно включить `pointer-events: all`.

## Что проверить

1. Откройте Spawn-меню — должна появиться вкладка **Inspect** с иконкой 🔍.
2. Переключитесь на эту вкладку — курсор должен меняться на «указатель» при наведении на объекты.
3. Нажмите на объект — он должен подсветиться синей обводкой (SelectedOutline).
4. Наведите на другой объект без клика — он должен подсветиться жёлтой обводкой (HoveredOutline).
5. Убедитесь, что правый клик по объекту вызывает контекстное меню.
