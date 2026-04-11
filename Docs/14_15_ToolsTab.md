# Этап 14_15 — ToolsTab и SpawnMenuModeBar

## Что мы делаем?

Создаём два компонента:

1. **ToolsTab** — вкладка «Tools» внутри спавн-меню. Слева — список всех инструментов (ToolMode), справа — панель настроек выбранного инструмента (ControlSheet).
2. **SpawnMenuModeBar** — горизонтальная панель **режимов** спавн-меню, отображаемая вверху экрана. Она позволяет переключаться между разными «режимами» всего спавн-меню (например, основной и сохранения).

### Аналогия

**ToolsTab** — как **панель инструментов в графическом редакторе**: слева список кистей/карандашей, справа — настройки выбранной кисти (размер, цвет, прозрачность).

**SpawnMenuModeBar** — как **вкладки браузера**, но для режимов меню: «Спавн», «Сохранения» и т.д.

---

## Как это работает внутри движка

### ToolsTab

1. `Game.TypeLibrary.GetTypes<ToolMode>()` — рефлексия движка: получает **все классы**, наследующие `ToolMode`. Каждый инструмент (Weld, Remover, Rope, Wheel и т.д.) — отдельный класс.
2. `GroupBy(x => x.Group)` — инструменты группируются по атрибуту `[Group]`.
3. `ControlSheet` — универсальный компонент движка, который **автоматически** генерирует UI для свойств объекта (слайдеры, чекбоксы, кнопки) на основе атрибутов `[Property]`.
4. `PostProcessManager` — система постобработки. Если выбраны компоненты постобработки, вместо настроек инструмента показываются **их** настройки.
5. `IUtilityTab` — интерфейс, отмечающий класс как вкладку утилит для спавн-меню.

### SpawnMenuModeBar

1. `SpawnMenuHost.SpawnMenuMode` — атрибут, которым помечаются классы-режимы спавн-меню.
2. `GetTypesWithAttribute<SpawnMenuHost.SpawnMenuMode>()` — находит все такие классы.
3. `CheckCondition()` — проверяет условие видимости (например, `HostOnly` — только для хоста).
4. `SpawnMenuHost.SwitchMode` — переключает текущий режим.

---

## Файлы этого этапа

| Путь | Назначение |
|------|-----------|
| `Code/UI/SpawnMenu/ToolsTab.razor` | Вкладка инструментов |
| `Code/UI/SpawnMenuModeBar.razor` | Панель режимов спавн-меню |
| `Code/UI/SpawnMenuModeBar.razor.scss` | Стили панели режимов |

---

## Полный код

### ToolsTab.razor

**Путь:** `Code/UI/SpawnMenu/ToolsTab.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox
@implements IUtilityTab
@attribute [Icon( "🔧" )]
@attribute [Title( "Tools" )]
@attribute [Order( -100 )]

<root class="tab">
	<div class="left">
        <VerticalMenu Value="@(CurrentMode?.GetType())"  class="menuinner">
            <Options>
                @foreach (var group in Game.TypeLibrary.GetTypes<ToolMode>().GroupBy(x => x.Group).OrderBy( x => x.Key ) )
                {
                    @if ( !string.IsNullOrWhiteSpace( group.Key ) )
                    {
                        <h2>@group.Key</h2>
                    }

                    @foreach (var type in group.OrderBy(x => x.Title))
                    {
                        if (type.IsAbstract) continue;

                        <MenuOption Text="@type.Title" Icon="@type.Icon" @onclick="@(() => SwitchMode(type))" Value="@type.TargetType"></MenuOption>
                    }
                }
            </Options>
        </VerticalMenu>
	</div>

    <div class="body menuinner">
        @if ( PostProcessManager?.GetSelectedComponents().Count > 0 )
        {
            @foreach ( var component in PostProcessManager.GetSelectedComponents() )
            {
                <h2>@(Game.TypeLibrary.GetType( component.GetType() )?.Title ?? component.GetType().Name)</h2>
                <ControlSheet Target="@component" PropertyFilter="@FilterProperties"></ControlSheet>
            }
        }
        else
        {
            <ControlSheet PropertyFilter="@FilterProperties" Target="@GetCurrentMode()"></ControlSheet>
        }
	</div>

</root>

@code
{
    ToolMode CurrentMode => Player.FindLocalPlayer()?.GetWeapon<Toolgun>()?.GetCurrentMode();
    PostProcessManager PostProcessManager => Game.ActiveScene.GetSystem<PostProcessManager>();

    protected override int BuildHash() => HashCode.Combine(CurrentMode, PostProcessManager?.SelectedPath, PostProcessManager?.GetSelectedComponents().Count);

	bool IsActiveMode( TypeDescription t )
	{
		var localPlayer = Player.FindLocalPlayer();
		var toolgun = localPlayer?.GetWeapon<Toolgun>();
		if (!toolgun.IsValid()) return false;

		return toolgun.GetCurrentMode()?.GetType() == t.TargetType;
	}

	void SwitchMode(TypeDescription t)
	{
		var localPlayer = Player.FindLocalPlayer();
		if ( localPlayer == null ) return;

		PostProcessManager?.Deselect();

		var inventory = localPlayer.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		inventory.SetToolMode( t.ClassName );
	}

	protected override void OnVisibilityChanged()
	{
		if ( !IsVisible )
		{
			GetCurrentMode()?.SaveCookies();
		}
	}

	ToolMode GetCurrentMode()
	{
		var localPlayer = Player.FindLocalPlayer();
		var toolgun = localPlayer?.GetWeapon<Toolgun>();
		return toolgun?.GetCurrentMode();
	}

	static bool FilterProperties(SerializedProperty o)
	{
		if (o.PropertyType is null) return false;
		if (o.PropertyType.IsAssignableTo(typeof(Delegate))) return false;

		if (o.IsMethod) return true;
		if (!o.HasAttribute<PropertyAttribute>()) return false;

		return true;
	}
}
```

---

### SpawnMenuModeBar.razor

**Путь:** `Code/UI/SpawnMenuModeBar.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits Panel

<root>

    <div class="menu-segmented-group">
    @{

        var activeMode = SpawnMenuHost.GetActiveMode();


        foreach ( var mode in Game.TypeLibrary.GetTypesWithAttribute<SpawnMenuHost.SpawnMenuMode>().OrderBy( x => x.Type.Order ) )
        {
            if ( !mode.Attribute.CheckCondition() ) continue;

            var activeClass = mode.Type.TargetType == activeMode?.GetType() ? "active" : "";

            <div class="menu-mode-button @activeClass" @onclick="@( () => SpawnMenuHost.SwitchMode( mode.Type.Name ) )">
                <div class="icon">@mode.Type.Icon</div>
                <div class="title">@mode.Type.Title</div>                
            </div>
        }
    }
    </div>

</root>

@code
{
    protected override int BuildHash() => HashCode.Combine( SpawnMenuHost.GetActiveMode(), Game.TypeLibrary.GetTypesWithAttribute<SpawnMenuHost.SpawnMenuMode>() );
}
```

---

### SpawnMenuModeBar.razor.scss

**Путь:** `Code/UI/SpawnMenuModeBar.razor.scss`

```scss

SpawnMenuModeBar
{
	position: absolute;
	top: 0;
	left: 0;
	right: 0;
	justify-content: center;
	align-items: center;
	padding: 3rem;
	gap: 1rem;
	z-index: 9999;
	background-image: linear-gradient( to bottom, rgba( black, 0.5 ), rgba( black, 0.0 ) );
}
```

---

## Разбор кода

### ToolsTab.razor — Вкладка инструментов

#### Атрибуты

```razor
@implements IUtilityTab
@attribute [Icon( "🔧" )]
@attribute [Title( "Tools" )]
@attribute [Order( -100 )]
```
- `@implements IUtilityTab` — помечает класс как **вкладку утилит**, чтобы движок включил её в систему вкладок спавн-меню.
- `Order(-100)` — **отрицательный** порядок, значит вкладка будет **первой** (левее остальных).

#### Левая панель — список инструментов

```razor
<VerticalMenu Value="@(CurrentMode?.GetType())" class="menuinner">
```
`VerticalMenu` — компонент вертикального меню. `Value` — текущее выделенное значение (тип активного инструмента).

```razor
@foreach (var group in Game.TypeLibrary.GetTypes<ToolMode>().GroupBy(x => x.Group).OrderBy( x => x.Key ))
```
Перебираем **все классы-инструменты**, группируем по `Group` (строка, задаётся атрибутом `[Group]` на классе инструмента) и сортируем по алфавиту.

```razor
@if ( !string.IsNullOrWhiteSpace( group.Key ) )
{
    <h2>@group.Key</h2>
}
```
Если у группы есть имя — рисуем **заголовок** (например, «Construction», «Fun»). Инструменты без группы идут без заголовка.

```razor
<MenuOption Text="@type.Title" Icon="@type.Icon" @onclick="@(() => SwitchMode(type))" Value="@type.TargetType"></MenuOption>
```
Каждый инструмент — пункт меню. `type.Title`, `type.Icon` — берутся из атрибутов класса. При клике — `SwitchMode`.

#### Правая панель — настройки

```razor
@if ( PostProcessManager?.GetSelectedComponents().Count > 0 )
```
Если в `PostProcessManager` есть выбранные компоненты (игрок кликнул на объект постобработки) — показываем **их** настройки. Иначе — настройки текущего инструмента.

```razor
<ControlSheet PropertyFilter="@FilterProperties" Target="@GetCurrentMode()"></ControlSheet>
```
`ControlSheet` — **автогенерируемая форма**. Движок:
1. Берёт объект `Target` (текущий ToolMode).
2. Перебирает его свойства.
3. Пропускает через `FilterProperties`.
4. Для каждого подходящего свойства рисует **соответствующий виджет** (слайдер для `float`, чекбокс для `bool`, кнопку для метода и т.д.).

#### Code-блок

```csharp
ToolMode CurrentMode => Player.FindLocalPlayer()?.GetWeapon<Toolgun>()?.GetCurrentMode();
```
**Цепочка вызовов** для получения текущего инструмента:
1. Найти локального игрока.
2. Получить его оружие типа `Toolgun`.
3. Получить активный режим (`ToolMode`).

```csharp
void SwitchMode(TypeDescription t)
```
Переключение инструмента:
1. Сбрасываем выделение постобработки (`PostProcessManager?.Deselect()`).
2. Через инвентарь вызываем `SetToolMode(t.ClassName)`.

```csharp
protected override void OnVisibilityChanged()
{
    if ( !IsVisible )
    {
        GetCurrentMode()?.SaveCookies();
    }
}
```
Когда вкладка **скрывается** (игрок переключился на другую или закрыл меню) — **сохраняем настройки** инструмента в cookies (локальное хранилище движка).

```csharp
static bool FilterProperties(SerializedProperty o)
```
Фильтр свойств для `ControlSheet`:
- Свойство должно иметь **тип** (`PropertyType is null` — пропускаем).
- **Делегаты** (события) — пропускаем.
- **Методы** (`IsMethod`) — показываем (кнопки действий).
- Обычные свойства — только с атрибутом `[Property]`.

---

### SpawnMenuModeBar.razor — Панель режимов

```razor
var activeMode = SpawnMenuHost.GetActiveMode();
```
Получаем **текущий активный режим** спавн-меню.

```razor
foreach ( var mode in Game.TypeLibrary.GetTypesWithAttribute<SpawnMenuHost.SpawnMenuMode>().OrderBy( x => x.Type.Order ) )
```
Перебираем **все классы**, помеченные атрибутом `SpawnMenuMode`, сортируем по порядку.

```razor
if ( !mode.Attribute.CheckCondition() ) continue;
```
`CheckCondition()` проверяет **условие видимости**. Например, `SpawnMenuMode<HostOnly>` — кнопка видна только хосту.

```razor
var activeClass = mode.Type.TargetType == activeMode?.GetType() ? "active" : "";
```
Если тип текущего режима совпадает с типом кнопки — добавляем CSS-класс `active` для подсветки.

```razor
<div class="menu-mode-button @activeClass" @onclick="@( () => SpawnMenuHost.SwitchMode( mode.Type.Name ) )">
    <div class="icon">@mode.Type.Icon</div>
    <div class="title">@mode.Type.Title</div>
</div>
```
Каждая кнопка — иконка + заголовок. При клике — `SwitchMode` с именем класса.

---

### SpawnMenuModeBar.razor.scss — Стили

```scss
SpawnMenuModeBar
{
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
```
Панель **прибита к верху экрана** и растянута по всей ширине.

```scss
    justify-content: center;
    align-items: center;
```
Кнопки **центрируются** по горизонтали и вертикали.

```scss
    padding: 3rem;
    gap: 1rem;
    z-index: 9999;
```
Большой `z-index` — панель поверх всего остального UI.

```scss
    background-image: linear-gradient( to bottom, rgba( black, 0.5 ), rgba( black, 0.0 ) );
```
**Градиент** от полупрозрачного чёрного к прозрачному — создаёт эффект затемнения сверху, чтобы кнопки были читаемы на любом фоне.

---

## Что проверить

1. Откройте спавн-меню — вкладка **Tools** (🔧) должна быть **первой** слева.
2. В левой панели — **сгруппированный список** инструментов с заголовками.
3. Кликните на любой инструмент — справа должны появиться **его настройки**.
4. Настройки генерируются **автоматически**: слайдеры, чекбоксы, кнопки.
5. Переключите инструмент — настройки справа должны **обновиться**.
6. Закройте меню — настройки должны **сохраниться** (при повторном открытии значения те же).
7. Вверху экрана (при открытом спавн-меню) — **панель режимов** с кнопками.
8. Кнопки режимов — с иконками и названиями, активная кнопка **подсвечена**.
9. Если вы хост — должны быть видны **дополнительные режимы** (например, Saves).
10. Градиент вверху — затемнение плавно **исчезает** книзу.
