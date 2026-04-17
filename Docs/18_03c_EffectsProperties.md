# 18.03c — Effects UI: EffectsProperties (панель свойств)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)
> - [00.27 — Razor стилизация](00_27_Razor_Styling.md)

## Что мы делаем?

Создаём **панель редактирования свойств** выбранного эффекта через `ControlSheet`. Это финальный компонент вкладки «Effects».

> Это третья из трёх частей урока 18.03. Предыдущие: [18.03a — EffectsHost](18_03a_EffectsHost.md), [18.03b — EffectsList](18_03b_EffectsList.md).

## Код EffectsProperties.razor

### Полный код

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    @if ( _selected.IsValid() )
    {
        <div class="header">
            <h2>
                <span>🎨</span>
                <span>@_selected.Title</span>
            </h2>
        </div>

        @if ( _properties.Count > 0 )
        {
            <ControlSheet Target="@_properties" />
        }
        else
        {
            <div class="empty-state">No editable properties.</div>
        }
    }
    else
    {
        <div class="empty-state">Select an effect to edit its properties.</div>
    }
</root>

@code
{
    PostProcessManager _manager;
    PostProcessResource _selected;
    List<SerializedProperty> _properties = new();

    public override void Tick()
    {
        if ( _manager is null )
            _manager = Game.ActiveScene.GetSystem<PostProcessManager>();

        var selectedPath = _manager?.SelectedPath;
        var resource = selectedPath != null
            ? ResourceLibrary.Get<PostProcessResource>( selectedPath )
            : null;

        if ( resource != _selected )
        {
            _selected = resource;
            RebuildProperties();
        }

        if ( _selected is not null )
            StateHasChanged();
    }

    void RebuildProperties()
    {
        var path = _selected?.ResourcePath;
        if ( path is null )
        {
            _properties = [];
            return;
        }

        var props = new List<SerializedProperty>();
        props.Add( TypeLibrary.CreateProperty( "Enabled",
            () => _manager?.IsEnabled( path ) ?? false,
            v => _manager?.Set( path, v ) ) );

        foreach ( var component in _manager?.GetSelectedComponents() ?? [] )
        {
            var so = TypeLibrary.GetSerializedObject( component );
            so.OnPropertyChanged = OnPropertyChanged;
            props.AddRange( so.Where( FilterProperties ) );
        }

        _properties = props;
    }

    static bool FilterProperties( SerializedProperty o )
    {
        if ( o.PropertyType is null ) return false;
        if ( o.PropertyType.IsAssignableTo( typeof(Delegate) ) ) return false;

        if ( o.IsMethod ) return true;
        if ( !o.HasAttribute<PropertyAttribute>() ) return false;

        return true;
    }

    void OnPropertyChanged( SerializedProperty prop )
    {
        foreach ( var target in prop.Parent.Targets )
        {
            if ( target is Component component )
                GameManager.ChangeProperty( component, prop.Name, prop.GetValue<object>() );
        }
    }

    protected override int BuildHash() => HashCode.Combine( _manager?.SelectedPath );
}
```

### Разбор кода

#### Разметка

Панель показывает одно из двух состояний:
1. **Эффект выбран** (`_selected.IsValid()`) — заголовок с названием и `ControlSheet` для редактирования свойств.
2. **Эффект не выбран** — заглушка «Select an effect to edit its properties.»

`ControlSheet` — встроенный компонент s&box, который принимает список `SerializedProperty` и автоматически создаёт подходящие элементы управления (слайдеры, чекбоксы, цветовые пикеры и т.д.).

#### Метод `Tick()`

```csharp
public override void Tick()
```

Вызывается каждый кадр. Проверяет, изменился ли выбранный эффект. Если да — перестраивает список свойств. Если эффект выбран — вызывает `StateHasChanged()` для обновления значений в реальном времени (слайдеры двигаются → значения обновляются).

#### Метод `RebuildProperties()`

```csharp
void RebuildProperties()
```

Строит список `SerializedProperty`:

1. Первым добавляет виртуальное свойство `"Enabled"` — чекбокс включения/выключения эффекта. Создаётся через `TypeLibrary.CreateProperty` с геттером и сеттером в виде лямбд.
2. Проходит по всем компонентам выбранного эффекта, извлекает сериализованные свойства и фильтрует их.
3. Подписывается на `OnPropertyChanged` для отслеживания изменений.

#### Фильтр свойств

```csharp
static bool FilterProperties( SerializedProperty o )
```

Пропускает:
- Свойства с `null`-типом — пропускает.
- Делегаты — пропускает.
- Методы (кнопки действий) — пропускает **через**.
- Свойства без атрибута `[Property]` — пропускает.

#### Обработка изменений

```csharp
void OnPropertyChanged( SerializedProperty prop )
```

При изменении свойства в UI вызывает `GameManager.ChangeProperty()`, чтобы изменение корректно зарегистрировалось в системе (для undo, синхронизации и т.д.).

---

## Код EffectsProperties.razor.scss

### Полный код

```scss
@import "/UI/Theme.scss";

EffectsProperties
{
    background-color: $menu-panel-bg;
    backdrop-filter: blur( 2px );
    border-radius: 8px;
    flex-direction: column;
    overflow: hidden;

    .header
    {
        padding: 10px 14px;
        flex-shrink: 0;

        h2
        {
            gap: 0.5rem;
            align-items: center;
            font-size: 14px;
            font-weight: 650;
            font-family: $subtitle-font;
            color: $menu-color;
        }
    }

    .empty-state
    {
        font-family: $subtitle-font;
        padding: 16px 12px;
        text-align: center;
        color: $menu-text;
        flex-grow: 1;
        align-items: center;
        justify-content: center;
    }

    ControlSheet
    {
        padding: 6px 0;
        flex-grow: 1;
        overflow-y: scroll;
    }
}
```

### Разбор кода

- **Корневой элемент `EffectsProperties`** — полупрозрачный фон с блюром и скруглёнными углами. `overflow: hidden` обрезает содержимое по границам.
- **`.header`** — заголовок с названием эффекта. `flex-shrink: 0` — не сжимается при нехватке места.
- **`h2`** — шрифт `$subtitle-font`, акцентный цвет `$menu-color`, `gap: 0.5rem` — отступ между эмоджи и текстом.
- **`.empty-state`** — заглушка, центрированная по обеим осям. `flex-grow: 1` — занимает всё доступное пространство.
- **`ControlSheet`** — `flex-grow: 1` + `overflow-y: scroll` — занимает оставшееся место и прокручивается, если свойств много.

---

---

## Что проверить

1. **Вкладка в меню спавна** — откройте меню спавна (клавиша Q по умолчанию). Должна появиться вкладка «Effects» с иконкой 🎨.
2. **Список эффектов** — во вкладке «Installed» должны отображаться все установленные `PostProcessResource`, сгруппированные по категориям с эмоджи.
3. **Предпросмотр при наведении** — наведите курсор на эффект в списке. Он должен временно включиться (видно на экране). При уходе курсора — выключиться.
4. **Переключение эффекта** — нажмите на чекбокс (✓) справа от эффекта. Он должен включиться/выключиться. Галочка окрашивается в акцентный цвет.
5. **Панель свойств** — нажмите на строку эффекта (не на чекбокс). Справа должна появиться панель с заголовком и редактируемыми свойствами (слайдеры, чекбоксы и т.д.).
6. **Чекбокс Enabled** — в панели свойств первым должен быть чекбокс «Enabled», управляющий включением/выключением эффекта.
7. **Вкладка Workshop** — переключитесь на «Workshop». Должен загрузиться список пакетов из мастерской с миниатюрами и именами авторов.
8. **Пресеты** — нажмите кнопку «Presets» в нижней части списка. Сохраните пресет, затем загрузите его — все эффекты и их свойства должны восстановиться.
9. **Стили** — убедитесь, что панели имеют полупрозрачный фон с блюром, вкладки переключаются с анимацией цвета, а выбранная строка подсвечена.


---

---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[19.01 — Context Menu Host (хост контекстного меню)](19_01_ContextMenuHost.md)** — начало следующей фазы.
