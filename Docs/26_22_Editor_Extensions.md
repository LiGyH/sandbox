# 26.22 — Editor-расширения: основы

## Что мы делаем?

Кроме игрового кода (`Code/`), у проекта может быть **код для редактора** (`Editor/`). Этот код **не попадает в билд**, но позволяет добавлять окна, инспекторы, контекстные меню, ассет-превью, тулзы. В этом руководстве мы уже используем editor-сборку (см. [25.01](25_01_Editor_Assembly.md)) — здесь систематизируем, что именно она умеет.

## Что такое `Editor`-сборка

В корне проекта есть две папки:

| Папка | Когда выполняется | Куда уходит |
|---|---|---|
| `Code/` | в редакторе и в билде | в финальную игру |
| `Editor/` | **только в редакторе** | в редакторские DLL |

Любой класс из `Editor/` имеет доступ к API редактора (`Sandbox.Editor.*`), но **не доступен** игровому коду. Это правильное разделение: тулзы не должны попадать в продакшен-билд.

## Основные точки расширения

### 1. Кастомный инспектор компонента — `[CustomEditor]`

Уже видели в [19.03](19_03_InspectorEditor.md). Позволяет нарисовать **своё** UI для компонента вместо стандартного «автогенерированного списка полей».

```csharp
[CustomEditor( typeof( WeaponResource ) )]
public class WeaponEditor : ResourceEditor
{
    public override void OnPaint() { ... }
}
```

### 2. Editor-приложение (окно)

Свои окна редактора — например, «Редактор брони», «Редактор диалогов»:

```csharp
[EditorApp( "Dialogue Editor", "chat", "Редактирование диалогов" )]
public class DialogueEditor : Window
{
    public DialogueEditor() { Title = "Dialogue Editor"; ... }
}
```

Окно появится в меню `View → Dialogue Editor`. Внутри — обычные `Sandbox.UI` элементы.

### 3. Контекстное меню в браузере ассетов

```csharp
[AssetContextMenu( typeof( SomethingAsset ) )]
public static void DoStuff( Asset asset )
{
    // действие из правого клика
}
```

### 4. Превью ассетов

Для своих кастомных ассетов ([26.21](26_21_Custom_Assets.md)) можно сделать **картинки-иконки** в браузере (`AssetPreview`).

### 5. Editor-события

`EditorEvent.OnSceneLoaded`, `EditorEvent.OnSelectionChanged` и другие — реакция на действия пользователя в редакторе.

### 6. Tools (точечные кнопки)

Кнопки на верхней панели, в инспекторе, в окне сцены:

```csharp
[EditorTool( "Place Cubes" )]
public class PlaceCubesTool : EditorTool { ... }
```

### 7. Component Editor Tools

Кнопки прямо в инспекторе компонента — «применить настройки», «сгенерировать данные», «открыть редактор». Очень удобно для дизайнерских туториалов.

```csharp
public class MyData : Component
{
    [Button( "Сгенерировать кривую" )]
    public void Regenerate() { ... }
}
```

Кнопка появится в инспекторе. Это уже доступно и из обычного `Code/` (см. [00.13](00_13_Component_Attributes.md)), но более сложные кнопки с диалогами лучше выносить в `Editor/`.

## Layout редактора (`Sandbox.UI` для tools)

В Editor-сборке UI тоже строится через Razor/Sandbox.UI, но классы немного другие — `Window`, `Widget`, `Layout`. Если хочешь нарисовать свой инспектор, начни с `Layout.Column()` / `Layout.Row()`, добавляй `Label`, `Button`, `LineEdit`, `ComboBox`.

```csharp
public class MyTool : Window
{
    public MyTool()
    {
        Layout = Layout.Column();
        Layout.Add( new Label( "Привет!" ) );
        var btn = Layout.Add( new Button( "Сделать" ) );
        btn.Clicked = () => Log.Info( "клик" );
    }
}
```

## Меню и шорткаты

```csharp
[Menu( "Tools/My Tool", Shortcut = "Ctrl+M" )]
public static void OpenMyTool() => new MyTool().Show();
```

Появится пункт меню `Tools → My Tool` с горячей клавишей. Шорткаты редактора видны в `Editor → Shortcuts`.

## Game Exporting и сборка

`Editor/` участвует в сборке редакторской DLL, но **не идёт в финальный пакет** при `Build → Compile Game`. Если случайно ссылаешься из `Code/` на `Editor/`-класс — **получишь ошибку компиляции в build mode**. Проверяй сборку.

## Полезные принципы

1. **Не клади игровую логику в Editor.** Игра не должна *работать* через редакторские тулзы.
2. **Тулзы должны быть идемпотентными.** Дважды нажатая кнопка не должна ломать данные.
3. **Не пиши в файлы вне проекта.** Тулзы редактируют ассеты только внутри папки проекта.
4. **Сложные тулзы документируй прямо в окне** (`<info>` блок), чтобы дизайнер не ломал голову.
5. **Не забывай про undo.** Если тулза меняет сцену, оборачивай в `Undo` ([editor/undo-system](https://github.com/Facepunch/sbox-docs/blob/master/docs/editor/undo-system.md)).

## Что важно запомнить

- `Editor/` — отдельная сборка, **не попадает в билд**, имеет доступ к редакторскому API.
- Атрибуты `[EditorApp]`, `[CustomEditor]`, `[AssetContextMenu]`, `[Menu]`, `[EditorTool]` — основные точки расширения.
- Простые «кнопки» в инспекторе можно делать через `[Button]` прямо в `Code/`.
- UI редактора строится из `Window`/`Widget`/`Layout` (Qt-подобный API).
- Не зови Editor-классы из `Code/` — это сломает сборку.
- Если изменяешь сцену тулзой — оборачивай в Undo.

## Что дальше?

В [26.23](26_23_Property_Attributes.md) — полный справочник `Property`-атрибутов, чтобы инспектор показывал твои поля красиво.
