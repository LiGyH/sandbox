# 19_04 — ComponentHandle (ручка компонента)

## Что мы делаем?

Создаём **`ComponentHandle`** — маленькую круглую иконку, которая «плавает» в мире поверх компонента и позволяет кликнуть по нему для выделения. Это UI-панель, позиционированная в экранных координатах, но привязанная к мировой позиции компонента.

## Как это работает внутри движка

1. `ContextMenuHost.DrawHandles()` находит все компоненты в радиусе 1000 юнитов от камеры.
2. Для каждого подходящего компонента (определяется `ShouldCreateHandle`) создаётся экземпляр `ComponentHandle`.
3. `ComponentHandle` — наследник `Panel`. Он хранит ссылку на `Component` и каждый кадр пересчитывает свою позицию на экране через `Scene.Camera.PointToScreenPixels`.
4. Иконка берётся из метаданных типа компонента: `TypeLibrary.GetType(c.GetType()).Icon`.
5. При клике по ручке вызывается `Inspector.SelectObject`, выделяя `GameObject` этого компонента.
6. Если компонент уничтожен — ручка автоматически удаляется.

Стили определяют круглый белый значок с тенью, голубой иконкой и анимацией при наведении/нажатии. При выделении — инвертированные цвета (синий фон, белая иконка). Класс `behind` скрывает ручку, если объект за камерой. Анимация `outro` плавно уменьшает значок при удалении.

## Путь к файлу

```
Code/UI/ContextMenu/ComponentHandle.cs
Code/UI/ContextMenu/ComponentHandle.cs.scss
```

## Полный код

### `Code/UI/ContextMenu/ComponentHandle.cs`

```csharp
﻿
using Sandbox.UI;
namespace Sandbox;

/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
public class ComponentHandle : Panel
{
	readonly Inspector _inspector;
	readonly Component _component;
	readonly Label _icon;
	Vector3 worldpos;

	public ComponentHandle( Panel parent, Component c, Inspector i ) : base( parent )
	{
		_component = c;
		_inspector = i;
		worldpos = _component.WorldPosition;

		_icon = AddChild<Label>( "icon" );

		var typeInfo = TypeLibrary.GetType( c.GetType() );
		_icon.Text = typeInfo.Icon ?? "ℹ️";
	}

	public override void Tick()
	{
		base.Tick();

		if ( _component.IsValid() )
		{
			worldpos = _component.WorldPosition;
		}

		{
			var screenPos = Scene.Camera.PointToScreenPixels( worldpos, out bool behind );
			Style.Left = screenPos.x * ScaleFromScreen;
			Style.Top = screenPos.y * ScaleFromScreen;

			SetClass( "behind", behind );
			//SetClass( "active", _inspector?.Selected == _component?.GameObject );
		}

		if ( ShouldDelete() )
		{
			Delete();
		}
	}

	bool ShouldDelete()
	{
		if ( !_component.IsValid() ) return true;

		return false;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		e.StopPropagation();

		_inspector.SelectObject( _component.GameObject );
	}

}
```

### `Code/UI/ContextMenu/ComponentHandle.cs.scss`

```scss
ComponentHandle
{
	width: 38px;
	height: 38px;
	background-color: #eee;
	box-shadow: 2px 2px 16px black;
	border-radius: 20px;
	position: absolute;
	justify-content: center;
	align-items: center;
	cursor: pointer;
	transform: scale( 1 ) translateX( -50% ) translateY( -50% );
	z-index: 100;

	.icon
	{
		font-family: "Material Icons";
		color: #08f;
		font-size: 23px;
		pointer-events: none;
	}

	&:hover
	{
		opacity: 1;
		background-color: #fff;
	}

	&:active
	{
		border: 1px solid #08f;
		background-color: #fff;
		opacity: 1;
		transform: scale( 0.95 ) translateX( -50% ) translateY( -50% );
	}

	&.active
	{
		border: 2px solid #fff;
		background-color: #08f;
		opacity: 1;

		.icon
		{
			color: #fff;
		}
	}

	&:outro
	{
		transform: scale( 0 ) translateX( -50% ) translateY( -50% );
		transition: transform 0.2s ease-out;
	}
}
```

## Разбор кода

### `ComponentHandle.cs` — класс

#### Поля

```csharp
readonly Inspector _inspector;
readonly Component _component;
readonly Label _icon;
Vector3 worldpos;
```

| Поле | Описание |
|------|----------|
| `_inspector` | Ссылка на `Inspector` — нужна для вызова `SelectObject` при клике. |
| `_component` | Компонент, к которому привязана ручка. Может быть любым наследником `Component`. |
| `_icon` | Дочерняя метка (`Label`) с иконкой из Material Icons. |
| `worldpos` | Кэшированная мировая позиция. Обновляется каждый кадр, если компонент жив. Если компонент уничтожен — позиция сохраняется на последнем месте. |

#### Конструктор

```csharp
public ComponentHandle( Panel parent, Component c, Inspector i ) : base( parent )
{
    _component = c;
    _inspector = i;
    worldpos = _component.WorldPosition;

    _icon = AddChild<Label>( "icon" );

    var typeInfo = TypeLibrary.GetType( c.GetType() );
    _icon.Text = typeInfo.Icon ?? "ℹ️";
}
```

1. Вызывает `base(parent)` — это стандартный паттерн s&box UI. Панель сразу становится дочерней к `parent` (обычно это `ContextMenuHost`).
2. Запоминает начальную мировую позицию.
3. Создаёт дочернюю метку с CSS-классом `"icon"`.
4. Через `TypeLibrary` получает метаданные типа компонента и извлекает иконку. Если иконка не указана — используется `"ℹ️"` как запасной вариант.

#### `Tick` — обновление каждый кадр

```csharp
if ( _component.IsValid() )
{
    worldpos = _component.WorldPosition;
}
```

Если компонент ещё существует — обновляем позицию. Если уничтожен — оставляем последнюю известную.

```csharp
var screenPos = Scene.Camera.PointToScreenPixels( worldpos, out bool behind );
Style.Left = screenPos.x * ScaleFromScreen;
Style.Top = screenPos.y * ScaleFromScreen;
SetClass( "behind", behind );
```

Проецируем мировую точку в пиксельные координаты экрана. `ScaleFromScreen` — коэффициент масштабирования UI (учитывает DPI, масштаб интерфейса). Если объект за камерой — добавляем класс `"behind"`, который скрывает ручку.

Закомментированная строка `SetClass("active", ...)` — заготовка для будущей подсветки, когда ручка соответствует выбранному объекту.

```csharp
if ( ShouldDelete() )
    Delete();
```

Если компонент уничтожен — панель удаляется. При удалении CSS-псевдокласс `:outro` запускает анимацию сжатия.

#### `ShouldDelete`

```csharp
bool ShouldDelete()
{
    if ( !_component.IsValid() ) return true;
    return false;
}
```

Простая проверка: если компонент больше не валиден (уничтожен, удалён) — пора удалять ручку. Это место для расширения (можно добавить проверку расстояния, видимости и т. д.).

#### Обработка клика

```csharp
protected override void OnMouseDown( MousePanelEvent e )
{
    e.StopPropagation();
    _inspector.SelectObject( _component.GameObject );
}
```

`StopPropagation()` — предотвращает всплытие события к `ContextMenuHost`, чтобы не происходил двойной выбор. Затем вызываем `SelectObject` на инспекторе, передавая `GameObject` этого компонента.

### Стили (SCSS)

#### Базовый стиль `ComponentHandle`

```scss
ComponentHandle
{
    width: 38px;
    height: 38px;
    background-color: #eee;
    box-shadow: 2px 2px 16px black;
    border-radius: 20px;
    position: absolute;
    justify-content: center;
    align-items: center;
    cursor: pointer;
    transform: scale( 1 ) translateX( -50% ) translateY( -50% );
    z-index: 100;
}
```

- **38×38 пикселей** — компактная круглая кнопка.
- **`border-radius: 20px`** — делает квадрат кругом (20 > 38/2 = 19, округлённо).
- **`position: absolute`** — позиционируется через `Style.Left` и `Style.Top` из кода.
- **`transform: translateX(-50%) translateY(-50%)`** — центрирует ручку на точке. Без этого левый верхний угол совпадал бы с мировой позицией.
- **`z-index: 100`** — поверх игрового мира, но ниже окон инспектора (`z-index: 200`).
- **`box-shadow`** — тень для визуального отделения от фона.

#### Иконка

```scss
.icon
{
    font-family: "Material Icons";
    color: #08f;
    font-size: 23px;
    pointer-events: none;
}
```

Текстовая метка стилизуется под иконку Material Icons. `pointer-events: none` — клики проходят сквозь иконку к самой ручке.

#### Состояния

| Состояние | Визуальный эффект |
|-----------|-------------------|
| `:hover` | Фон белый (`#fff`), полная непрозрачность |
| `:active` (нажатие) | Голубая рамка, лёгкое уменьшение (`scale(0.95)`) |
| `.active` (выделен) | Инвертированные цвета: голубой фон (`#08f`), белая иконка, белая рамка |
| `:outro` (удаление) | Плавное уменьшение до `scale(0)` за 0.2 секунды |

**`:outro`** — специальный псевдокласс s&box UI. Когда панель вызывает `Delete()`, движок не удаляет её мгновенно, а сначала применяет стили `:outro` и ждёт завершения анимации. Это позволяет плавно скрывать элементы.

### Связь с `ContextMenuHost.DrawHandles()`

В `ContextMenuHost`:

```csharp
void DrawHandles()
{
    var objects = Scene.FindInPhysics(new Sphere(Scene.Camera.WorldPosition, 1000));
    var rootObjects = objects.Select(x => x.Network?.RootGameObject)
                             .Where(x => x.IsValid()).Distinct();

    foreach (var o in rootObjects)
    {
        foreach (var component in o.GetComponentsInChildren<Component>())
        {
            if (!ShouldCreateHandle(component))
                continue;

            if (_handles.TryGetValue(component, out var existingHandle))
            {
                if (existingHandle.IsValid())
                    continue;
            }

            var panel = new ComponentHandle(this, component, Inspector);
            _handles[component] = panel;
        }
    }
}
```

Словарь `_handles` (тип `Dictionary<Component, Panel>`) гарантирует, что для каждого компонента создаётся максимум одна ручка. Если ручка уже существует и валидна — пропускаем. Если была удалена — создаём новую.

> **Примечание:** `ShouldCreateHandle` сейчас всегда возвращает `false`. Ручки не создаются в текущей версии — это подготовка для будущего функционала (например, отображение иконок источников света, камер и других специальных компонентов).

## Что проверить

1. Убедитесь, что класс `ComponentHandle` компилируется без ошибок.
2. Если вы измените `ShouldCreateHandle` в `ContextMenuHost`, чтобы он возвращал `true` для определённых компонентов (например, `component is PointLight`), над соответствующими объектами должны появиться круглые иконки.
3. Проверьте, что иконки следуют за объектом при его перемещении.
4. Проверьте, что иконки скрываются, когда объект оказывается за камерой.
5. Кликните по ручке — объект должен выделиться в инспекторе.
6. Уничтожьте объект — ручка должна плавно исчезнуть (анимация `:outro`).
