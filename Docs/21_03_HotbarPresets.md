# 21_03 — Пресеты хотбара (HotbarPresetsButton)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём кнопку-меню для управления пресетами (предустановками) инвентаря игрока. Она позволяет:

- Сохранять текущую раскладку хотбара как именованный пресет.
- Быстро переключаться между сохранёнными пресетами.
- Перезаписывать существующий пресет текущей раскладкой.
- Удалять ненужные пресеты.
- Сбрасывать хотбар к раскладке по умолчанию.

## Как это работает внутри движка

- `PlayerInventory` — компонент, управляющий инвентарём игрока. Метод `GetLoadoutPresets()` возвращает список сохранённых пресетов из `LocalData`.
- `LocalData` — локальное хранилище s&box на клиенте. Данные сохраняются между сессиями.
- `MenuPanel.Open(this)` — открывает контекстное меню, привязанное к текущей панели.
- `StringQueryPopup` — встроенное диалоговое окно для ввода текстовой строки (имени пресета).
- Каждый пресет хранится как JSON-строка раскладки хотбара.

## Путь к файлу

```
Code/UI/Inventory/HotbarPresetsButton.razor
Code/UI/Inventory/HotbarPresetsButton.razor.scss
```

## Полный код

### `HotbarPresetsButton.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@namespace Sandbox
@inherits Panel

<root>

    <div class="presets-toggle" @onclick=@OnToggleClick>
        <i>expand_less</i>
    </div>

</root>

@code
{
    public PlayerInventory Inventory { get; set; }

    void OnToggleClick()
    {
        var menu = MenuPanel.Open( this );

        var presets = PlayerInventory.GetLoadoutPresets();
        foreach ( var preset in presets )
        {
            var name = preset.Name;
            var json = preset.LoadoutJson;
            menu.AddSubmenu( "bookmark", name, sub =>
            {
                sub.AddOption( "play_arrow", "Load", () => OnLoadPreset( json ) );
                sub.AddOption( "save", "Overwrite with current", () => OnOverwritePreset( name ) );
                sub.AddOption( "close", "Delete", () => OnDeletePreset( name ) );
            } );
        }

        if ( presets.Any() )
        {
            menu.AddSpacer();
        }

        menu.AddOption( "refresh", "Reset to Default", ResetToDefault );
        menu.AddOption( "add", "New Preset", OnSaveNew );
        menu.StateHasChanged();
    }

    void OnLoadPreset( string json )
    {
        if ( !Inventory.IsValid() ) return;
        Inventory.SwitchToPreset( json );
    }

    void OnOverwritePreset( string name )
    {
        var json = LocalData.Get<string>( "hotbar" );
        if ( string.IsNullOrEmpty( json ) ) return;

        PlayerInventory.SaveLoadoutPreset( name, json );
    }

    void OnDeletePreset( string name )
    {
        PlayerInventory.DeleteLoadoutPreset( name );
    }

    void ResetToDefault()
    {
        if ( !Inventory.IsValid() ) return;
        Inventory.ResetToDefault();
    }

    void OnSaveNew()
    {
        var popup = new StringQueryPopup
        {
            Title = "New Inventoy Preset",
            Placeholder = "Enter a name...",
            ConfirmLabel = "Save",
            OnConfirm = OnSaveConfirmed,
            Parent = FindRootPanel()
        };
    }

    void OnSaveConfirmed( string name )
    {
        var json = LocalData.Get<string>( "hotbar" );
        if ( string.IsNullOrEmpty( json ) ) return;

        PlayerInventory.SaveLoadoutPreset( name, json );
    }
}
```

### `HotbarPresetsButton.razor.scss`

```scss
@import "/UI/Theme.scss";

HotbarPresetsButton
{
    position: relative;
    right: 0px;
    bottom: 0px;
    opacity: 0;
}

HotbarPresetsButton .presets-toggle
{
    width: 28px;
    height: 96px;
    background-color: #0003;
    border-radius: $hud-radius;
    justify-content: center;
    align-items: center;
    cursor: pointer;
    color: rgba( white, 0.5 );
    font-size: 20px;
    pointer-events: all;
    transition: background-color 0.1s, color 0.1s;

    &:hover
    {
        background-color: #0006;
        color: white;
    }
}
```

## Разбор кода

### Razor-разметка

```html
<div class="presets-toggle" @onclick=@OnToggleClick>
    <i>expand_less</i>
</div>
```

Минимальная кнопка — иконка `expand_less` (стрелка вверх) из Material Icons. При нажатии открывается контекстное меню.

### Свойство `Inventory`

```csharp
public PlayerInventory Inventory { get; set; }
```

Ссылка на компонент инвентаря игрока. Устанавливается родительским компонентом при создании.

### Метод `OnToggleClick` — главная логика меню

1. **Открытие меню**: `MenuPanel.Open(this)` создаёт контекстное меню, привязанное к кнопке.

2. **Список пресетов**: для каждого сохранённого пресета создаётся подменю с тремя действиями:
   - **Load** (`play_arrow`) — загрузить пресет.
   - **Overwrite with current** (`save`) — заменить пресет текущей раскладкой.
   - **Delete** (`close`) — удалить пресет.

3. **Разделитель**: `menu.AddSpacer()` — визуальная линия между пресетами и остальными опциями (только если есть пресеты).

4. **Системные опции**:
   - **Reset to Default** (`refresh`) — сбросить хотбар к начальному состоянию.
   - **New Preset** (`add`) — сохранить текущую раскладку как новый пресет.

### Загрузка пресета

```csharp
void OnLoadPreset( string json )
{
    if ( !Inventory.IsValid() ) return;
    Inventory.SwitchToPreset( json );
}
```

Проверяет валидность инвентаря и передаёт JSON-строку раскладки в `SwitchToPreset`.

### Перезапись пресета

```csharp
void OnOverwritePreset( string name )
{
    var json = LocalData.Get<string>( "hotbar" );
    if ( string.IsNullOrEmpty( json ) ) return;
    PlayerInventory.SaveLoadoutPreset( name, json );
}
```

Читает текущую раскладку хотбара из `LocalData` по ключу `"hotbar"` и сохраняет под существующим именем.

### Создание нового пресета

```csharp
void OnSaveNew()
{
    var popup = new StringQueryPopup
    {
        Title = "New Inventoy Preset",
        Placeholder = "Enter a name...",
        ConfirmLabel = "Save",
        OnConfirm = OnSaveConfirmed,
        Parent = FindRootPanel()
    };
}
```

Открывает модальное окно для ввода имени. После подтверждения вызывается `OnSaveConfirmed`, который аналогично сохраняет JSON из `LocalData`.

### SCSS-стили

- **HotbarPresetsButton**: `opacity: 0` — кнопка изначально невидима (предполагается, что родитель управляет видимостью при наведении).
- **.presets-toggle**: узкая вертикальная кнопка (28×96px) с полупрозрачным фоном. При наведении становится ярче.
- `pointer-events: all` — гарантирует, что кнопка кликабельна, даже если родитель блокирует события.
- `transition` — плавные переходы цвета фона и текста за 0.1 секунды.

## Что проверить

1. Наведите курсор на область хотбара — должна появиться кнопка со стрелкой.
2. Нажмите кнопку — должно открыться меню с опциями.
3. Создайте пресет «Боевой набор» → переключите раскладку → загрузите пресет → хотбар должен вернуться к сохранённому состоянию.
4. Перезапишите пресет новым набором → загрузите → должен загрузиться обновлённый набор.
5. Удалите пресет → он должен исчезнуть из меню.
6. Нажмите **Reset to Default** → хотбар должен вернуться к начальной раскладке.


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[22.01 — NPC: Основной класс (Npc) 🤖](22_01_Npc.md)** — начало следующей фазы.
