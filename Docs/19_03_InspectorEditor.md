# 19_03 — InspectorEditor и InspectorEditorAttribute

## Что мы делаем?

Создаём интерфейс **`IInspectorEditor`** и атрибут **`InspectorEditorAttribute`** — систему плагинов для инспектора. Любой разработчик может создать свою вкладку инспектора, просто написав класс-панель, реализующий `IInspectorEditor` и пометив его атрибутом `[InspectorEditor]`. Движок автоматически найдёт все такие классы и покажет их как вкладки.

## Как это работает внутри движка

Система работает по принципу **«соглашение через атрибуты»** (convention over configuration):

1. Разработчик создаёт панель (наследник `Panel`), реализующую `IInspectorEditor`.
2. Помечает её `[InspectorEditor(typeof(SomeType))]` или `[InspectorEditor(null)]` для общего назначения.
3. Опционально — `[Order(N)]` для управления порядком вкладок.
4. Класс `Inspector` при инициализации через `TypeLibrary.GetTypesWithAttribute<InspectorEditorAttribute>()` автоматически находит все такие типы, создаёт их экземпляры и добавляет в систему вкладок.

Интерфейс `IInspectorEditor` определяет контракт:
- `TrySetTarget(List<GameObject>)` — редактор получает текущее выделение и решает, может ли он что-то показать. Возвращает `true`, если вкладку нужно отобразить.
- `Title` — заголовок вкладки для панели с табами.

Атрибут `InspectorEditorAttribute` хранит необязательный `Type` — тип компонента, для которого предназначен редактор. Если `null` — это редактор общего назначения (как `GameObjectInspector`).

## Путь к файлу

```
Code/UI/ContextMenu/InspectorEditor.cs
Code/UI/ContextMenu/InspectorEditorAttribute.cs
```

## Полный код

### `Code/UI/ContextMenu/InspectorEditor.cs`

```csharp
namespace Sandbox;

public interface IInspectorEditor
{
	public bool TrySetTarget( List<GameObject> selection );
	public string Title { get; }
}
```

### `Code/UI/ContextMenu/InspectorEditorAttribute.cs`

```csharp
namespace Sandbox;

/// <summary>
/// Put this on a panel implementing <see cref="IInspectorEditor"/> to register it with the inspector.
/// </summary>
[AttributeUsage( AttributeTargets.Class )]
public class InspectorEditorAttribute : Attribute
{
	public Type Type { get; }

	public InspectorEditorAttribute( Type type = null )
	{
		Type = type;
	}
}
```

## Разбор кода

### `IInspectorEditor` — интерфейс редактора

```csharp
public interface IInspectorEditor
{
    public bool TrySetTarget( List<GameObject> selection );
    public string Title { get; }
}
```

Это минимальный контракт для вкладки инспектора:

| Член | Описание |
|------|----------|
| `TrySetTarget(List<GameObject> selection)` | Вызывается каждый кадр из `Inspector.UpdateEditors()`. Принимает текущий список выделенных объектов. Редактор должен: (1) обновить своё внутреннее состояние, (2) вернуть `true`, если он может отобразить что-то полезное для данного выделения, или `false`, если ему нечего показать. |
| `Title` | Строка, отображаемая на вкладке. Может быть динамической — например, `GameObjectInspector` показывает количество выделенных объектов в заголовке. |

**Важно:** интерфейс не наследует `Panel`, но `Inspector` при инициализации проверяет `if (editor is not Panel editorPanel) continue;` — то есть реализация обязательно должна быть панелью. Это сделано намеренно: интерфейс лёгкий и не привязан к UI-системе, но на практике нужен `Panel`.

### `InspectorEditorAttribute` — атрибут регистрации

```csharp
[AttributeUsage( AttributeTargets.Class )]
public class InspectorEditorAttribute : Attribute
{
    public Type Type { get; }

    public InspectorEditorAttribute( Type type = null )
    {
        Type = type;
    }
}
```

| Элемент | Описание |
|---------|----------|
| `[AttributeUsage(AttributeTargets.Class)]` | Атрибут применяется только к классам. |
| `Type` | Тип компонента, для которого предназначен этот редактор. Если `null` — редактор общего назначения, применимый к любым объектам. |
| Конструктор с `type = null` | Параметр необязательный: можно написать `[InspectorEditor]` или `[InspectorEditor(typeof(Light))]`. |

### Как `Inspector` использует эти типы

В `Inspector.InitEditors()`:

```csharp
var types = TypeLibrary.GetTypesWithAttribute<InspectorEditorAttribute>()
    .OrderByDescending( x => x.Type.GetAttribute<OrderAttribute>()?.Value ?? 0 )
    .ThenBy( t => t.Attribute.Type is null ? 1 : 0 );
```

1. `TypeLibrary.GetTypesWithAttribute<InspectorEditorAttribute>()` — находит все типы во всех загруженных сборках, помеченные `[InspectorEditor]`.
2. Сортировка: сначала по `[Order]` (убывание — больший Order выше), затем специализированные редакторы (`Type != null`) перед общими (`Type == null`).
3. Для каждого найденного типа создаётся экземпляр через `typeDesc.Create<IInspectorEditor>()`.
4. Если он реализует `Panel` — добавляется в `.window-stack` с CSS-классом `window`.

### Пример создания своего редактора

Допустим, вы хотите добавить вкладку для компонентов `Light`:

```razor
@using Sandbox;
@using Sandbox.UI;
@attribute [InspectorEditor(typeof(Light))]
@attribute [Order(200)]
@inherits Panel
@namespace Sandbox
@implements IInspectorEditor

<root>
    <div class="body">
        <label>💡 Настройки света</label>
        <!-- ваш UI -->
    </div>
</root>

@code
{
    public string Title => "💡 Light";

    public bool TrySetTarget(List<GameObject> selection)
    {
        // Показываем вкладку, только если есть объект с Light
        return selection.Any(go => go.GetComponent<Light>() != null);
    }
}
```

Этот класс будет автоматически обнаружен и добавлен как вкладка инспектора.

### Порядок сортировки

Сортировка двухуровневая:

1. **`OrderByDescending(Order)`** — вкладки с бо́льшим значением `[Order]` идут первыми. `GameObjectInspector` имеет `[Order(100)]`.
2. **`ThenBy(Type is null)`** — при одинаковом Order, специализированные редакторы (с конкретным типом) идут перед общими (с `null`).

Это гарантирует, что если есть редактор, специально предназначенный для `ModelRenderer`, он появится до общего «📦 Object».

## Что проверить

1. Убедитесь, что файл `InspectorEditor.cs` содержит интерфейс `IInspectorEditor` с двумя членами: `TrySetTarget` и `Title`.
2. Убедитесь, что `InspectorEditorAttribute.cs` содержит атрибут с параметром `Type`.
3. Проверьте, что `GameObjectInspector` корректно помечен `[InspectorEditor(null)]` и `@implements IInspectorEditor`.
4. В игре: при выделении объекта вкладки инспектора должны автоматически показывать только те редакторы, которые вернули `true` из `TrySetTarget`.
5. Если вы создадите новый класс с `[InspectorEditor]` — он должен автоматически появиться как вкладка без изменений в `Inspector`.
