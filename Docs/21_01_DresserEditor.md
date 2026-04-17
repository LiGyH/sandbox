# 21_01 — Редактор одежды (DresserEditor)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём панель-редактор одежды для NPC и персонажей в инспекторе. Эта панель позволяет в реальном времени рандомизировать одежду на модели или полностью её очистить. Все изменения синхронизируются по сети — другие игроки тоже видят обновлённый внешний вид.

## Как это работает внутри движка

- Атрибут `[InspectorEditor(typeof(Dresser))]` регистрирует этот Razor-компонент как кастомный редактор для компонента `Dresser` в инспекторе s&box.
- Интерфейс `IInspectorEditor` требует реализовать метод `TrySetTarget`, который вызывается при выборе объекта в редакторе.
- Методы `TryBroadcastRandomize` и `TryBroadcastClear` помечены атрибутом `[Rpc.Host]` — это означает, что вызов идёт на хост-сервер, а результат (обновление сети через `go.Network.Refresh()`) виден всем клиентам.
- Компонент `Dresser` отвечает за применение предметов одежды к скелетной модели. Метод `Randomize()` выбирает случайный набор, а `Clear()` + `Apply()` — убирает всё.

## Путь к файлу

```
Code/UI/Dresser/DresserEditor.razor
Code/UI/Dresser/DresserEditor.razor.scss
```

## Полный код

### `DresserEditor.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@attribute [InspectorEditor(typeof(Dresser))]
@inherits Panel
@namespace Sandbox
@implements IInspectorEditor

<root>
    <div class="body">
        <Label>todo: add / remove clothing resources</Label>
    </div>
    <div class="footer">
            <Button class="menu-action primary" Text="Randomize" Icon="🎲" onclick=@DoRandomize></Button>
            <Button class="menu-action primary" Text="Clear" Icon="🧹" onclick=@DoClear></Button>
    </div>
</root>

@code
{
    public string Title => "👗 Dresser";

    public Dresser Target { get; private set; }

    public bool TrySetTarget(List<GameObject> selection)
    {
        Dresser found = null;
        foreach (var go in selection)
        {
            if ( go.Tags.Has( "player" ) ) continue;
            found = go.GetComponentInChildren<Dresser>();
            if (found != null) break;
        }

        if (found == Target) return Target != null;

        Target = found;
        StateHasChanged();
        return Target != null;
    }

    void DoRandomize() => TryBroadcastRandomize( Target.GameObject );
    void DoClear() => TryBroadcastClear( Target.GameObject );

    [Rpc.Host]
    private static void TryBroadcastRandomize(GameObject go)
    {
        if ( !go.IsValid() ) return;

        var dresser = go.GetComponentInChildren<Dresser>();
        if ( dresser is null ) return;

        dresser.Randomize();

        // Refresh the object to update the clothing items for everyone
        go.Network.Refresh();
    }

    [Rpc.Host]
    private static async void TryBroadcastClear(GameObject go)
    {
        if ( !go.IsValid() ) return;

        var dresser = go.GetComponentInChildren<Dresser>();
        if ( dresser is null ) return;

        dresser.Clothing.Clear();
        dresser.Clear();
        await dresser.Apply();

        // Refresh the object to update the clothing items for everyone
        go.Network.Refresh();
    }
}
```

### `DresserEditor.razor.scss`

```scss
@import "/UI/Theme.scss";

DresserEditor
{
    flex-direction: column;
    width: 100%;
    justify-content: flex-end;
}
```

## Разбор кода

### Razor-разметка

| Элемент | Назначение |
|---|---|
| `<Label>todo: add / remove clothing resources</Label>` | Заглушка — здесь планируется UI для выбора конкретных предметов одежды |
| `<Button ... Text="Randomize" ... onclick=@DoRandomize>` | Кнопка «Рандомизировать» — генерирует случайный набор одежды |
| `<Button ... Text="Clear" ... onclick=@DoClear>` | Кнопка «Очистить» — снимает всю одежду с модели |

### Свойство `Title`

```csharp
public string Title => "👗 Dresser";
```

Заголовок панели в инспекторе. Эмодзи делает вкладку заметной.

### Метод `TrySetTarget`

```csharp
public bool TrySetTarget(List<GameObject> selection)
```

- Перебирает выделенные объекты.
- Пропускает объекты с тегом `"player"` — редактор одежды предназначен для NPC.
- Ищет компонент `Dresser` в дочерних объектах.
- Если цель изменилась — обновляет `Target` и вызывает `StateHasChanged()` для перерисовки UI.

### RPC-методы

**`TryBroadcastRandomize`** — статический метод с атрибутом `[Rpc.Host]`:
1. Проверяет валидность `GameObject`.
2. Находит `Dresser` в дочерних объектах.
3. Вызывает `Randomize()` — случайная одежда.
4. `go.Network.Refresh()` — синхронизирует состояние по сети.

**`TryBroadcastClear`** — аналогично, но асинхронный (`async`):
1. Очищает список одежды через `dresser.Clothing.Clear()`.
2. Вызывает `dresser.Clear()` для удаления визуальных элементов.
3. `await dresser.Apply()` — асинхронно применяет изменения.
4. Синхронизирует по сети.

### SCSS-стили

Простая колоночная раскладка (`flex-direction: column`) на всю ширину панели. Контент прижимается к низу (`justify-content: flex-end`).

## Что проверить

1. Выделите NPC с компонентом `Dresser` в сцене — панель должна появиться в инспекторе.
2. Нажмите **Randomize** — одежда NPC должна измениться на случайную.
3. Нажмите **Clear** — вся одежда должна исчезнуть.
4. Подключите второго клиента и убедитесь, что изменения одежды видны обоим игрокам.
5. Убедитесь, что панель не появляется при выделении игрока (тег `"player"`).


---

## ➡️ Следующий шаг

Переходи к **[21.02 — Редактор выражения лица (FacePoseEditor)](21_02_FacePoseEditor.md)**.
