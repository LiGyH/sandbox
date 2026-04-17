# 20_04 — Элемент управления: Диалог ввода строки (StringQueryPopup) 💬

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём **StringQueryPopup** — универсальное всплывающее окно для ввода строкового значения. Это модальный диалог с заголовком, необязательным подзаголовком, полем ввода текста и кнопками «Подтвердить» / «Отмена». Может также использоваться как простой диалог подтверждения (без поля ввода).

Этот компонент используется повсеместно: переименование объектов, ввод значений, подтверждение действий.

## Как это работает внутри движка

- `StringQueryPopup` наследуется от `Panel` (не `BasePopup`), но реализует попап-поведение самостоятельно: кликом по фону (backdrop) закрывается.
- Настраивается через публичные свойства: `Title`, `Prompt`, `Placeholder`, `ConfirmLabel`, `InitialValue`, `ShowInput`.
- Если `ShowInput = false`, поле ввода скрывается, и попап работает как диалог подтверждения (confirm/cancel).
- Кнопка подтверждения блокируется (`Disabled`), если `ShowInput = true` и поле пустое — нельзя подтвердить пустую строку.
- При подтверждении вызывается делегат `OnConfirm` с обрезанным значением (`Value?.Trim()`).
- `OnAfterTreeRender(firstTime)` инициализирует `Value` из `InitialValue` после первого рендера, чтобы поле ввода сразу содержало предзаполненное значение.
- Корневой элемент имеет класс `"popup"`, а внутренняя панель помечена `onclick:preventDefault`, чтобы клик внутри не закрывал попап.

## Путь к файлам

```
Code/UI/Controls/StringQueryPopup.razor
Code/UI/Controls/StringQueryPopup.razor.scss
```

## Полный код

### `StringQueryPopup.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root class="popup">

    <div class="inner" onclick:preventDefault=@true>
        <div class="popup-header">
            <h2>@Title</h2>
        </div>

        @if ( !string.IsNullOrEmpty( Prompt ) )
        {
            <h3>@Prompt</h3>
        }

        @if ( ShowInput )
        {
            <TextEntry class="menu-input" Placeholder=@Placeholder Value:bind=@Value />
        }

        <span class="grow">
            <Button Text=@ConfirmLabel Icon="check" class="menu-action primary" Disabled=@(ShowInput && string.IsNullOrWhiteSpace( Value )) @onclick=@OnConfirmClick />
            <Button Text="Cancel" Icon="close" class="menu-action primary" @onclick=@(() => Delete()) />
        </span>
    </div>

</root>

@code
{
    /// <summary>Title shown at the top of the popup.</summary>
    public string Title { get; set; } = "Enter Value";

    /// <summary>Optional subtitle/prompt below the title.</summary>
    public string Prompt { get; set; }

    /// <summary>Placeholder text for the text entry.</summary>
    public string Placeholder { get; set; } = "Enter a value...";

    /// <summary>Label for the confirm button.</summary>
    public string ConfirmLabel { get; set; } = "Confirm";

    /// <summary>Pre-filled value in the text entry.</summary>
    public string InitialValue { get; set; } = "";

    /// <summary>When false, hides the text entry — acts as a simple confirm/deny dialog.</summary>
    public bool ShowInput { get; set; } = true;

    /// <summary>Called with the trimmed string when the user confirms.</summary>
    public Action<string> OnConfirm { get; set; }

    string Value { get; set; }

    protected override void OnAfterTreeRender( bool firstTime )
    {
        if ( firstTime )
            Value = InitialValue;
    }

    void OnConfirmClick()
    {
        if ( ShowInput && string.IsNullOrWhiteSpace( Value ) ) return;
        OnConfirm?.Invoke( Value?.Trim() ?? "" );
        Delete();
    }

    protected override void OnMouseDown( MousePanelEvent e )
    {
        base.OnMouseDown( e );

        // Click on the backdrop (not the inner panel) dismisses the popup
        if ( e.Target == this )
            Delete();
    }
}
```

### `StringQueryPopup.razor.scss`

```scss
@import "/UI/Popup.scss";

StringQueryPopup .inner
{
	width: 800px;
}
```

## Разбор кода

### StringQueryPopup.razor

| Элемент | Описание |
|---------|----------|
| `@inherits Panel` | Наследуется от базового `Panel`, а не от `BasePopup`. Попап-поведение реализовано вручную (клик по фону закрывает). |
| `class="popup"` | CSS-класс `popup` на корне. Импортируемый `Popup.scss` задаёт полноэкранный полупрозрачный фон-оверлей. |
| `onclick:preventDefault=@true` | Директива на `.inner` — предотвращает всплытие клика, чтобы клик внутри диалога не закрывал его. |
| **Свойство `Title`** | Заголовок попапа, по умолчанию `"Enter Value"`. Отображается в `<h2>` внутри `.popup-header`. |
| **Свойство `Prompt`** | Необязательный подзаголовок/подсказка. Рендерится как `<h3>` только если не пустой. |
| **Свойство `Placeholder`** | Текст-заглушка в поле ввода (виден, пока поле пустое). По умолчанию `"Enter a value..."`. |
| **Свойство `ConfirmLabel`** | Текст на кнопке подтверждения, по умолчанию `"Confirm"`. Позволяет менять на «Rename», «Save» и т.д. |
| **Свойство `InitialValue`** | Начальное значение поля ввода. Устанавливается в `OnAfterTreeRender` после первого рендера. |
| **Свойство `ShowInput`** | `true` — показывает поле ввода (режим ввода строки). `false` — скрывает поле (режим подтверждения действия). |
| **Свойство `OnConfirm`** | Делегат `Action<string>`, вызываемый при нажатии кнопки подтверждения. Получает обрезанную строку. |
| `Value` | Приватное свойство, привязанное к `TextEntry` через `Value:bind=@Value`. Двусторонняя привязка — при вводе текста значение обновляется автоматически. |
| `TextEntry` | Встроенный компонент движка — поле ввода текста. `Placeholder` показывает серый текст при пустом поле. |
| `Button` | Встроенный компонент кнопки. `Icon="check"` / `Icon="close"` — Material Icons. `class="menu-action primary"` — стиль из темы. |
| `Disabled=@(...)` | Кнопка подтверждения заблокирована, если `ShowInput = true` и `Value` пустое. Предотвращает подтверждение с пустым вводом. |
| `OnAfterTreeRender(firstTime)` | Хук жизненного цикла Razor. При `firstTime = true` устанавливает `Value = InitialValue`. Это необходимо, потому что `TextEntry` может ещё не существовать на момент установки свойств. |
| `OnConfirmClick()` | Двойная проверка пустого значения, вызов делегата, закрытие попапа через `Delete()`. |
| `OnMouseDown()` | Обработчик клика. `e.Target == this` проверяет, был ли клик по самому попапу (фону-оверлею), а не по внутренней панели `.inner`. Если да — закрывает попап. |
| `Delete()` | Метод `Panel.Delete()` — удаляет панель из дерева UI, фактически закрывая попап. |

### StringQueryPopup.razor.scss

| Элемент | Описание |
|---------|----------|
| `@import "/UI/Popup.scss"` | Подключает общие стили попапов: полноэкранный оверлей, центрирование внутренней панели, скруглённые углы, тени. |
| `StringQueryPopup .inner` | Задаёт ширину внутренней панели 800px. Высота определяется автоматически по содержимому. |

### Пример использования

```csharp
var popup = new StringQueryPopup();
popup.Title = "Переименование";
popup.Prompt = "Введите новое имя объекта:";
popup.Placeholder = "Имя объекта...";
popup.ConfirmLabel = "Переименовать";
popup.InitialValue = currentName;
popup.ShowInput = true;
popup.Parent = FindPopupPanel();
popup.OnConfirm = ( newName ) =>
{
    myObject.Name = newName;
};
```

### Пример использования как диалога подтверждения

```csharp
var popup = new StringQueryPopup();
popup.Title = "Удаление";
popup.Prompt = "Вы уверены, что хотите удалить этот объект?";
popup.ConfirmLabel = "Удалить";
popup.ShowInput = false;
popup.Parent = FindPopupPanel();
popup.OnConfirm = ( _ ) =>
{
    myObject.Delete();
};
```

## Что проверить

1. Создайте `StringQueryPopup` с `ShowInput = true` — поле ввода должно отображаться с плейсхолдером.
2. Введите текст — кнопка подтверждения должна стать активной.
3. Очистите поле — кнопка должна снова заблокироваться.
4. Нажмите «Confirm» — делегат `OnConfirm` должен получить обрезанную строку, попап закрывается.
5. Нажмите «Cancel» — попап закрывается без вызова `OnConfirm`.
6. Кликните по фону (за пределами `.inner`) — попап закрывается.
7. Создайте попап с `ShowInput = false` — поле ввода должно отсутствовать, кнопка подтверждения всегда активна.
8. Передайте `InitialValue = "test"` — поле ввода должно содержать текст «test» при открытии.


---

## ➡️ Следующий шаг

Переходи к **[20.05 — Система перетаскивания (Drag & Drop) 🖱️](20_05_DragDrop.md)**.
