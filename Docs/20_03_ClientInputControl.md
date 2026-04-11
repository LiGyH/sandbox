# 20_03 — Элемент управления: Привязка ввода (ClientInputControl) 🎮

## Что мы делаем?

Создаём **ClientInputControl** — кастомный контрол инспектора для свойств типа `ClientInput`. Он показывает текущую привязку клавиши (иконку кнопки/клавиши и название действия), а при клике открывает выпадающее меню со всеми зарегистрированными действиями ввода, сгруппированными по категориям.

Этот контрол позволяет дизайнерам привязать любое действие ввода к компоненту прямо из инспектора — без написания кода.

## Как это работает внутри движка

- `ClientInputControl` помечен `[CustomEditor(typeof(ClientInput))]` — движок подставляет его для всех свойств типа `ClientInput`.
- Контрол наследуется от `BaseControl` и строит UI программно в конструкторе (не через Razor-разметку, а через C#-код).
- В конструкторе создаются дочерние элементы: `_preview` (контейнер с `InputHint` и иконкой-заглушкой) и `_bindLabel` (текст названия действия).
- `InputHint` — встроенный компонент движка, который автоматически отображает правильную иконку клавиши/кнопки геймпада для указанного действия.
- `Rebuild()` берёт текущее значение `ClientInput.Action` и обновляет UI: если действие не задано, показывается иконка клавиатуры и текст «No Binding», иначе — иконка конкретной клавиши и название действия.
- `SupportsMultiEdit = true` означает, что контрол может одновременно редактировать одно свойство у нескольких выбранных компонентов.
- По клику открывается `MenuPanel` со списком всех действий из `Input.GetActions()`. Действия группируются по `GroupName` — группы без имени отображаются как плоский список, именованные группы — как подменю.
- `ActionLabel()` формирует текст пункта меню: название действия + текущая привязка клавиши в скобках (через `Input.GetButtonOrigin()`).
- `OnBindChanged()` обновляет `ClientInput.Action`, устанавливает значение через `Property.SetValue()`, и дополнительно уведомляет `GameManager.ChangeProperty()` для корректной синхронизации (обходной путь — прямое обновление не всегда работает).

## Путь к файлам

```
Code/UI/Controls/ClientInputControl.cs
Code/UI/Controls/ClientInputControl.cs.scss
```

## Полный код

### `ClientInputControl.cs`

```csharp

namespace Sandbox.UI;

[CustomEditor( typeof( ClientInput ) )]
public partial class ClientInputControl : BaseControl
{
	Panel _preview;
	InputHint _inputHint;
	IconPanel _fallbackIcon;
	Label _bindLabel;

	public override bool SupportsMultiEdit => true;

	public ClientInputControl()
	{
		_preview = AddChild<Panel>( "preview" );
		_inputHint = _preview.AddChild<InputHint>( "hint" );
		_fallbackIcon = _preview.AddChild<IconPanel>( "fallback" );
		_fallbackIcon.Text = "keyboard";

		_bindLabel = AddChild<Label>( "bind-label" );
	}

	public override void Rebuild()
	{
		if ( Property == null ) return;

		var action = Property.GetValue<ClientInput>().Action;

		if ( string.IsNullOrWhiteSpace( action ) )
		{
			_inputHint.Action = null;
			_inputHint.SetClass( "hidden", true );
			_fallbackIcon.SetClass( "hidden", false );
			_bindLabel.Text = "No Binding";
			SetClass( "no-binding", true );
			return;
		}

		_inputHint.Action = action;
		_inputHint.SetClass( "hidden", false );
		_fallbackIcon.SetClass( "hidden", true );
		SetClass( "no-binding", false );

		var match = Input.GetActions().FirstOrDefault( a => a.Name == action );
		_bindLabel.Text = match != null ? (match.Title ?? match.Name) : action;
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );

		var menu = Sandbox.MenuPanel.Open( this );

		menu.AddOption( "", "No Binding", () => OnBindChanged( "" ) );
		menu.AddSpacer();

		var grouped = Input.GetActions()
			.GroupBy( a => a.GroupName ?? "" )
			.OrderBy( g => g.Key );

		foreach ( var group in grouped )
		{
			if ( string.IsNullOrWhiteSpace( group.Key ) )
			{
				foreach ( var action in group )
				{
					var a = action;
					menu.AddOption( "", ActionLabel( a ), () => OnBindChanged( a.Name ) );
				}
			}
			else
			{
				var groupActions = group.ToList();
				menu.AddSubmenu( "", group.Key, sub =>
				{
					foreach ( var action in groupActions )
					{
						var a = action;
						sub.AddOption( "", ActionLabel( a ), () => OnBindChanged( a.Name ) );
					}
				} );
			}
		}
	}

	string ActionLabel( InputAction a )
	{
		var title = !string.IsNullOrEmpty( a.Title ) ? a.Title : a.Name;
		var origin = Input.GetButtonOrigin( a.Name );
		return origin != null ? $"{title} ({origin})" : title;
	}

	void OnBindChanged( string value )
	{
		var current = Property.GetValue<ClientInput>();
		current.Action = value;
		Property.SetValue( current );

		// tony: when setting Action in current, and setting the value, the property changed event doesn't show the updated action
		// not too sure why

		foreach ( var target in Property.Parent?.Targets ?? Enumerable.Empty<object>() )
		{
			if ( target is Component component )
				GameManager.ChangeProperty( component, Property.Name, current );
		}

		Rebuild();
	}
}
```

### `ClientInputControl.cs.scss`

```scss
@import 'resource-picker.scss';

ClientInputControl
{
	@include resource-picker-base;

	&.no-binding
	{
		background-color: #0088ff0a;
		border-color: #0088ff22;

		&:hover
		{
			background-color: #0088ff22;
			border-color: #0088ff44;
		}
	}

	.preview
	{
		width: 2.5rem;
		height: 2.5rem;
		flex-shrink: 0;
		align-items: center;
		justify-content: center;

		.hint
		{
			width: 100%;
			height: 100%;
			background-size: contain;
			background-repeat: no-repeat;
			background-position: center center;

			&.hidden { display: none; }
		}

		.fallback
		{
			font-size: 1.1rem;
			color: rgba( 255, 255, 255, 0.5 );

			&.hidden { display: none; }
		}
	}

	.bind-label
	{
		flex-grow: 1;
		font-size: 0.95rem;
		font-weight: 600;
		color: #fff;
	}
}
```

## Разбор кода

### ClientInputControl.cs

| Элемент | Описание |
|---------|----------|
| `[CustomEditor(typeof(ClientInput))]` | Регистрирует контрол как редактор для всех свойств типа `ClientInput`. |
| `partial class` | Класс помечен `partial`, что позволяет движку генерировать дополнительный код (например, для привязки стилей из `.scss`-файла). |
| `SupportsMultiEdit` | Возвращает `true` — контрол корректно работает при одновременном выборе нескольких объектов в инспекторе. |
| **Конструктор** | Строит UI программно: создаёт `Panel` для превью, внутри него `InputHint` (иконка клавиши) и `IconPanel` (запасная иконка «keyboard»). Отдельно создаёт `Label` для текста привязки. |
| `_preview` | Контейнер-панель с классом `"preview"`. |
| `_inputHint` | Компонент `InputHint` — встроенный элемент движка, который по имени действия автоматически показывает иконку нужной клавиши/кнопки. |
| `_fallbackIcon` | `IconPanel` с иконкой `"keyboard"` — показывается, когда привязка не задана. |
| `_bindLabel` | `Label` с классом `"bind-label"` — отображает название привязанного действия. |
| `Rebuild()` | Получает `ClientInput.Action` из свойства. Если пусто — показывает заглушку. Иначе — ищет действие в `Input.GetActions()` и берёт его `Title`. |
| `SetClass("no-binding", true)` | Добавляет CSS-класс на корневой элемент. При отсутствии привязки контрол выглядит более приглушённым. |
| `OnClick()` | Открывает контекстное меню через `MenuPanel.Open(this)`. Первый пункт — «No Binding» (сброс). Дальше — все действия, сгруппированные по `GroupName`. |
| `GroupBy(a => a.GroupName)` | Группирует действия. Безымянные группы рисуются плоско, именованные — как подменю (`AddSubmenu`). |
| `ActionLabel()` | Формирует текст пункта: `"Jump (Space)"`, `"Attack (Mouse1)"`. `Input.GetButtonOrigin()` возвращает физическое имя кнопки для текущего устройства ввода. |
| `OnBindChanged()` | Обновляет `ClientInput.Action`. Затем итерирует по всем целевым объектам свойства (`Property.Parent?.Targets`) и вызывает `GameManager.ChangeProperty()` для каждого компонента — это обходной путь для корректного уведомления системы о изменении. |
| `var a = action` | Захват переменной цикла в локальную переменную — классический паттерн C# для корректной работы замыканий в циклах. |

### ClientInputControl.cs.scss

| Селектор | Описание |
|----------|----------|
| `@include resource-picker-base` | Использует базовый миксин (но не `resource-picker-control`!), т.к. у этого контрола своя структура превью (не миниатюра ресурса, а иконка клавиши). |
| `&.no-binding` | Состояние без привязки — более прозрачный фон и рамка, визуально «пустой» контрол. |
| `.preview` | Квадрат 2.5rem для иконки клавиши. |
| `.hint` | Стили для `InputHint` — растягивается на весь `.preview`, `background-size: contain` сохраняет пропорции иконки. |
| `.hint.hidden` / `.fallback.hidden` | Скрывает элемент через `display: none`. Только один из двух виден в любой момент. |
| `.bind-label` | Занимает оставшееся пространство (`flex-grow: 1`), белый текст с жирным начертанием. |

## Что проверить

1. Создайте компонент с свойством `[Property] public ClientInput MyAction { get; set; }` — в инспекторе должен появиться контрол с иконкой клавиатуры и текстом «No Binding».
2. Кликните по контролу — должно открыться меню со всеми действиями ввода.
3. Выберите действие (например, «Jump») — иконка должна смениться на Space, текст — на «Jump».
4. Выберите «No Binding» — контрол вернётся в приглушённое состояние.
5. Проверьте работу с несколькими выделенными объектами — все должны получить одинаковую привязку.
6. Убедитесь, что действия правильно группируются в подменю.
