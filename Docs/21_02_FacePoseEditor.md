# 21_02 — Редактор выражения лица (FacePoseEditor)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём полноценный редактор морфов лица (face morphs) для скелетных моделей. Панель отображается в инспекторе при выделении объекта с компонентом `SkinnedModelRenderer`. Она позволяет:

- Настраивать каждый морф через ползунки (сила + сторона для парных морфов).
- Рандомизировать все морфы.
- Сбрасывать выражение лица в ноль.
- Сохранять, загружать и удалять именованные пресеты.

Состоит из двух компонентов: **FacePoseEditor** (главная панель) и **FacePoseMorphRow** (строка с ползунками для одного морфа).

## Как это работает внутри движка

- `SkinnedModelRenderer` — компонент, отвечающий за рендеринг скелетной модели. Свойство `Morphs` даёт доступ к массиву морфов.
- `SceneModel.Morphs.Get/Set/Reset` — прямое чтение и запись значений морфов на уровне сцены (немедленно видно визуально).
- `GameManager.ApplyMorphBatch` / `ApplyFacePosePreset` — сетевые методы, которые синхронизируют морфы с другими клиентами.
- Пресеты хранятся в `LocalData` — локальное хранилище на клиенте, привязанное к конкретной модели.
- Парные морфы (L/R) — например, `browL` и `browR` — автоматически группируются. Один ползунок управляет силой, второй — балансом между левой и правой стороной.

## Путь к файлу

```
Code/UI/FacePoser/FacePoseEditor.razor
Code/UI/FacePoser/FacePoseEditor.razor.scss
Code/UI/FacePoser/FacePoseMorphRow.razor
Code/UI/FacePoser/FacePoseMorphRow.razor.scss
```

## Полный код

### `FacePoseEditor.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@attribute [InspectorEditor(typeof(SkinnedModelRenderer))]
@inherits Panel
@namespace Sandbox
@implements IInspectorEditor

<root>
    <div class="body">
            @foreach (var group in Groups)
            {
                <div class="group-header">@group.Key</div>
                @foreach (var row in group)
                {
                    <FacePoseMorphRow Target=@Target
                                      NameA=@row.NameA
                                      NameB=@row.NameB
                                      Title=@row.Title
                                      OnMorphChanged=@QueueMorphChange
                                      class=@( IsRowActive( row ) ? "active" : "" )>
                    </FacePoseMorphRow>
                }
            }
    </div>

    <div class="footer">
            <Button class="menu-action primary" Text="Reset" Icon="🧹" onclick=@ClearAll></Button>
            <Button class="menu-action primary" Text="Randomize" Icon="🎲" onclick=@Randomize></Button>
            <Button class="menu-action primary" Text="Presets" Icon="📋" onclick=@OpenPresetsMenu></Button>
    </div>
</root>

@code
{
    public string Title => "😶 Face Poser";

    public SkinnedModelRenderer Target { get; private set; }

    Dictionary<string, float> _pendingMorphs = new();
    TimeSince _lastMorphEdit;

    void QueueMorphChange( string name, float value )
    {
        _pendingMorphs[name] = value;
        _lastMorphEdit = 0;
    }

    void FlushPendingMorphs()
    {
        if ( _pendingMorphs.Count == 0 || Target == null ) return;
        if ( _lastMorphEdit < 0.2f ) return;

        GameManager.ApplyMorphBatch( Target, Sandbox.Json.Serialize( _pendingMorphs ) );
        _pendingMorphs.Clear();
    }

    public override void Tick()
    {
        FlushPendingMorphs();
    }

    public bool TrySetTarget(List<GameObject> selection)
    {
        SkinnedModelRenderer smr = null;
        foreach (var go in selection)
        {
            var found = go.GetComponentInChildren<SkinnedModelRenderer>();
            if (found?.Morphs?.Names?.Length > 0) { smr = found; break; }
        }

        if (smr == Target) return Target != null;

        Target = smr;
        StateHasChanged();
        return Target != null;
    }

    record MorphRow(string NameA, string NameB, string Title);

    IEnumerable<IGrouping<string, MorphRow>> Groups => BuildGroups();

    List<MorphRow> BuildRows()
    {
        if (Target?.Morphs?.Names is not { Length: > 0 } names)
            return new();

        var unprocessed = names.ToList();
        var rows = new List<MorphRow>();

        foreach (var group in unprocessed.GroupBy(x => x[..^1]).ToArray())
        {
            if (group.Count() != 2) continue;

            var lower = group.Select(x => x.ToLower());
            if (!lower.Any(x => x.EndsWith('l')) || !lower.Any(x => x.EndsWith('r')))
                continue;

            unprocessed.RemoveAll(x => group.Contains(x));
            var sorted = group.Order().ToArray();
            rows.Add(new MorphRow(sorted[0], sorted[1], FormatTitle(group.Key)));
        }

        foreach (var name in unprocessed)
            rows.Add(new MorphRow(name, null, FormatTitle(name)));

        return rows;
    }

    IEnumerable<IGrouping<string, MorphRow>> BuildGroups() => BuildRows().GroupBy(r => GetGroup(r.Title)).OrderBy(g => g.Key);

    bool IsRowActive(MorphRow row) => Target.IsValid() && (
        (Target.SceneModel.Morphs.Get(row.NameA) > 0f) ||
        (row.NameB != null && (Target.SceneModel.Morphs.Get(row.NameB) > 0f))
    );

    void ClearAll()
    {
        if (!Target.IsValid() || Target.Morphs?.Names == null) return;

        _pendingMorphs.Clear();

        foreach (var name in Target.Morphs.Names)
            Target.SceneModel.Morphs.Reset(name);

        var zeroed = Target.Morphs.Names.ToDictionary( n => n, _ => 0f );
        GameManager.ApplyFacePosePreset( Target, Sandbox.Json.Serialize( zeroed ) );
        StateHasChanged();
    }

    void Randomize()
    {
        if (!Target.IsValid() || Target.Morphs?.Names is not { Length: > 0 }) return;

        _pendingMorphs.Clear();

        var morphs = new Dictionary<string, float>();

        foreach (var row in BuildRows())
        {
            if (row.NameB != null)
            {
                var strength = Random.Shared.Float(0, 1);
                var side = Random.Shared.Float(-1, 1);
                morphs[row.NameA] = strength * side.Remap(0, -1, 1, 0).Clamp(0, 1);
                morphs[row.NameB] = strength * side.Remap(0, 1, 1, 0).Clamp(0, 1);
            }
            else
            {
                morphs[row.NameA] = Random.Shared.Float(0, 1);
            }
        }

        foreach (var (name, val) in morphs)
            Target.SceneModel.Morphs.Set(name, val);

        GameManager.ApplyFacePosePreset( Target, Sandbox.Json.Serialize( morphs ) );
        StateHasChanged();
    }

    record FacePosePreset( string Name, Dictionary<string, float> Morphs );
    record FacePosePresetList( List<FacePosePreset> Presets );

    string PresetKey => $"faceposer/{System.IO.Path.GetFileNameWithoutExtension( Target?.Model?.Name ?? "unknown" )}";

    FacePosePresetList LoadPresetList() => LocalData.Get<FacePosePresetList>( PresetKey, new FacePosePresetList( new() ) );

    void SaveNewPreset( string name )
    {
        if ( !Target.IsValid() || Target.Morphs?.Names == null ) return;

        var morphs = Target.Morphs.Names.ToDictionary( n => n, n => Target.SceneModel.Morphs.Get( n ) );
        var list = LoadPresetList();
        list.Presets.RemoveAll( p => p.Name == name );
        list.Presets.Add( new FacePosePreset( name, morphs ) );
        LocalData.Set( PresetKey, list );
        StateHasChanged();
    }

    void ApplyPreset( FacePosePreset preset )
    {
        if ( !Target.IsValid() ) return;

        foreach ( var (name, val) in preset.Morphs )
            Target.SceneModel.Morphs.Set( name, val );

        GameManager.ApplyFacePosePreset( Target, Sandbox.Json.Serialize( preset.Morphs ) );
        StateHasChanged();
    }

    void DeletePreset( string name )
    {
        var list = LoadPresetList();
        list.Presets.RemoveAll( p => p.Name == name );
        if ( list.Presets.Count == 0 )
            LocalData.Delete( PresetKey );
        else
            LocalData.Set( PresetKey, list );
        StateHasChanged();
    }

    void OpenPresetsMenu()
    {
        var menu = MenuPanel.Open( this );

        menu.AddOption( "save", "Save New Preset...", () =>
        {
            var popup = new StringQueryPopup
            {
                Title = "Save Preset",
                Prompt = "Enter a name for this preset.",
                Placeholder = "Preset name...",
                ConfirmLabel = "Save",
                OnConfirm = name => SaveNewPreset( name )
            };
            popup.Parent = FindPopupPanel();
        } );

        var list = LoadPresetList();
        if ( list.Presets.Count > 0 )
        {
            menu.AddSpacer();
            foreach ( var preset in list.Presets )
            {
                var captured = preset;
                menu.AddSubmenu( "auto_awesome", captured.Name, sub =>
                {
                    sub.AddOption( "play_arrow", "Load", () => ApplyPreset( captured ) );
                    sub.AddOption( "delete", "Delete", () => DeletePreset( captured.Name ) );
                } );
            }
        }
    }

    static string FormatTitle(string name)
    {
        name = name.Replace("lower", "Lower").Replace("upper", "Upper")
        .Replace("raiser", "Raiser").Replace("inflate", "Inflate")
        .Replace("bulge", "Bulge").Replace("suck", "Suck")
        .Replace("thrust", "Thrust").Replace("sideways", "Sideways")
        .Replace("depressor", "Depressor").Replace("corner", "Corner")
        .Replace("puller", "Puller").Replace("pucker", "Pucker")
        .Replace("wrinkle", "Wrinkle").Replace("jaw", "Jaw")
        .Replace("mouth", "Mouth");
        return name.ToTitleCase();
    }

    static string GetGroup(string title)
    {
        if (title.Contains("brow", StringComparison.OrdinalIgnoreCase)) return "Eyes";
        if (title.Contains("eye", StringComparison.OrdinalIgnoreCase)) return "Eyes";
        if (title.Contains("cheek", StringComparison.OrdinalIgnoreCase)) return "Face";
        if (title.Contains("nose", StringComparison.OrdinalIgnoreCase)) return "Face";
        if (title.Contains("nostril", StringComparison.OrdinalIgnoreCase)) return "Face";
        if (title.Contains("chin", StringComparison.OrdinalIgnoreCase)) return "Face";
        if (title.Contains("jaw", StringComparison.OrdinalIgnoreCase)) return "Mouth";
        if (title.Contains("lip", StringComparison.OrdinalIgnoreCase)) return "Mouth";
        if (title.Contains("mouth", StringComparison.OrdinalIgnoreCase)) return "Mouth";
        return "Misc";
    }

    protected override int BuildHash() => HashCode.Combine(Target);
}
```

### `FacePoseEditor.razor.scss`

```scss
@import "/UI/Theme.scss";

FacePoseEditor
{
    flex-direction: column;
    width: 100%;
    max-height: 400px;
}

FacePoseEditor .group-header
{
    font-size: 11px;
    font-weight: 700;
    color: rgba( 255, 255, 255, 0.35 );
    padding: 6px 6px 2px 6px;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    flex-shrink: 0;
}
```

### `FacePoseMorphRow.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
    <div class="label @(IsActive ? "active" : "")">@Title</div>
    <div class="sliders">
        <SliderControl Property=@_so?.GetProperty(nameof(Strength)) ShowTextEntry=@(false) ShowValueTooltip=@(false)></SliderControl>
        @if (IsPaired)
        {
            <SliderControl Property=@_so?.GetProperty(nameof(Side)) ShowTextEntry=@(false) ShowValueTooltip=@(false)></SliderControl>
        }
    </div>
</root>

@code
{
    [Parameter] public SkinnedModelRenderer Target { get; set; }
    [Parameter] public string NameA { get; set; }
    [Parameter] public string NameB { get; set; }
    [Parameter] public string Title { get; set; }
    [Parameter] public Action<string, float> OnMorphChanged { get; set; }

    bool IsPaired => NameB != null;
    bool IsActive => (Target?.SceneModel?.Morphs.Get(NameA) ?? 0f) > 0f || (IsPaired && (Target?.SceneModel?.Morphs.Get(NameB) ?? 0f) > 0f);

    SerializedObject _so;

    // Live helpers — always read from the rendered mesh so sliders stay in sync
    float LiveStrength()
    {
        if (!Target.IsValid()) return 0f;
        var l = Target.SceneModel.Morphs.Get(NameA);
        var r = IsPaired ? Target.SceneModel.Morphs.Get(NameB) : 0f;
        return MathF.Max(l, r);
    }

    float LiveSide()
    {
        if (!Target.IsValid() || !IsPaired) return 0f;
        var l = Target.SceneModel.Morphs.Get(NameA);
        var r = Target.SceneModel.Morphs.Get(NameB);
        if (r > l) return l == 0 ? -1f : -(1f - l / r);
        if (l > r) return r == 0 ? 1f : 1f - r / l;
        return 0f;
    }

    [Range(0f, 1f), Step(0.01f)]
    public float Strength
    {
        get => LiveStrength();
        set
        {
            if (!Target.IsValid()) return;
            var side = LiveSide();
            if (IsPaired)
            {
                var valA = value * side.Remap(0, -1, 1, 0).Clamp(0, 1);
                var valB = value * side.Remap(0, 1, 1, 0).Clamp(0, 1);
                Target.SceneModel.Morphs.Set(NameA, valA);
                Target.SceneModel.Morphs.Set(NameB, valB);
                OnMorphChanged?.Invoke(NameA, valA);
                OnMorphChanged?.Invoke(NameB, valB);
            }
            else
            {
                Target.SceneModel.Morphs.Set(NameA, value);
                OnMorphChanged?.Invoke(NameA, value);
            }
        }
    }

    [Range(-1f, 1f), Step(0.01f)]
    public float Side
    {
        get => LiveSide();
        set
        {
            if (!Target.IsValid() || !IsPaired) return;
            var strength = LiveStrength();
            var valA = strength * value.Remap(0, -1, 1, 0).Clamp(0, 1);
            var valB = strength * value.Remap(0, 1, 1, 0).Clamp(0, 1);
            Target.SceneModel.Morphs.Set(NameA, valA);
            Target.SceneModel.Morphs.Set(NameB, valB);
            OnMorphChanged?.Invoke(NameA, valA);
            OnMorphChanged?.Invoke(NameB, valB);
        }
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _so ??= TypeLibrary.GetSerializedObject(this);
    }

    public void Clear()
    {
        if (!Target.IsValid()) return;
        Target.Morphs.Clear(NameA);
        if (IsPaired) Target.Morphs.Clear(NameB);
        StateHasChanged();
    }

    protected override int BuildHash() => HashCode.Combine(Target, NameA, NameB);
}
```

### `FacePoseMorphRow.razor.scss`

```scss
@import "/UI/Theme.scss";

FacePoseMorphRow
{
    flex-direction: row;
    align-items: center;
    padding: 2px 6px;
    min-height: 24px;
    border-radius: 4px;

    &:hover
    {
        background-color: rgba( 255, 255, 255, 0.04 );
    }
}

FacePoseMorphRow .label
{
    width: 130px;
    flex-shrink: 0;
    font-size: 12px;
    color: rgba( 255, 255, 255, 0.35 );
    white-space: nowrap;
    text-overflow: ellipsis;
    overflow: hidden;

    &.active
    {
        color: rgba( 255, 255, 255, 0.9 );
    }
}

FacePoseMorphRow .sliders
{
    flex-direction: row;
    flex-grow: 1;
    gap: 4px;
}

FacePoseMorphRow SliderControl
{
    flex-grow: 1;
    height: 18px;
}
```

## Разбор кода

### FacePoseEditor — главная панель

#### Выбор цели (`TrySetTarget`)

```csharp
public bool TrySetTarget(List<GameObject> selection)
```

Перебирает выделенные объекты, ищет `SkinnedModelRenderer` с непустым массивом морфов (`Morphs.Names.Length > 0`). Если модель не имеет морфов — панель не появится.

#### Построение строк (`BuildRows`)

Алгоритм автоматически определяет парные морфы (левый/правый):

1. Группирует имена морфов по всем символам кроме последнего (`x[..^1]`).
2. Если в группе ровно 2 элемента и они заканчиваются на `L` и `R` — это пара.
3. Пары объединяются в одну строку `MorphRow(NameA, NameB, Title)`.
4. Одиночные морфы становятся строками с `NameB = null`.

#### Группировка (`GetGroup`)

Морфы распределяются по визуальным группам на основе ключевых слов в названии:

| Ключевые слова | Группа |
|---|---|
| `brow`, `eye` | Eyes |
| `cheek`, `nose`, `nostril`, `chin` | Face |
| `jaw`, `lip`, `mouth` | Mouth |
| Всё остальное | Misc |

#### Батчинг сетевых вызовов

Чтобы не отправлять RPC при каждом движении ползунка, используется буфер `_pendingMorphs`:

```csharp
void QueueMorphChange( string name, float value )
{
    _pendingMorphs[name] = value;
    _lastMorphEdit = 0;
}
```

Каждый тик (`Tick`) проверяется, прошло ли 0.2 секунды с последнего изменения. Если да — весь батч отправляется одним вызовом `ApplyMorphBatch`.

#### Пресеты

- `PresetKey` — ключ хранения, уникальный для каждой модели (по имени файла модели).
- `SaveNewPreset` — считывает текущие значения морфов и сохраняет в `LocalData`.
- `ApplyPreset` — применяет сохранённые значения и синхронизирует по сети.
- `DeletePreset` — удаляет пресет; если пресетов не осталось, удаляет весь ключ.
- `OpenPresetsMenu` — создаёт контекстное меню с опциями «Save», «Load», «Delete».

### FacePoseMorphRow — строка одного морфа

#### Параметры

| Параметр | Тип | Описание |
|---|---|---|
| `Target` | `SkinnedModelRenderer` | Целевая модель |
| `NameA` | `string` | Имя первого морфа (или единственного) |
| `NameB` | `string` | Имя второго морфа (для пар, иначе `null`) |
| `Title` | `string` | Отображаемое название строки |
| `OnMorphChanged` | `Action<string, float>` | Колбэк при изменении значения |

#### Два ползунка

- **Strength** (0..1) — общая сила морфа. Для парных морфов управляет максимумом из L/R.
- **Side** (-1..1) — баланс между левым и правым (только для парных). `-1` = только правый, `0` = оба одинаково, `1` = только левый.

#### Формулы пересчёта

При изменении `Strength` с текущим `Side`:

```csharp
valA = value * side.Remap(0, -1, 1, 0).Clamp(0, 1);
valB = value * side.Remap(0, 1, 1, 0).Clamp(0, 1);
```

`Remap` — линейная интерполяция из одного диапазона в другой. Это позволяет плавно распределять силу между двумя сторонами.

#### `SerializedObject`

```csharp
_so ??= TypeLibrary.GetSerializedObject(this);
```

Создаёт `SerializedObject` для текущего экземпляра, чтобы `SliderControl` мог привязаться к свойствам `Strength` и `Side` через рефлексию.

### SCSS-стили

- **FacePoseEditor**: колоночная раскладка, максимальная высота 400px (с прокруткой). Заголовки групп — маленький серый текст в верхнем регистре.
- **FacePoseMorphRow**: горизонтальная строка с подсветкой при наведении. Метка фиксированной ширины 130px, тусклая если морф неактивен, яркая если активен.

## Что проверить

1. Выделите объект с `SkinnedModelRenderer` и морфами — панель «😶 Face Poser» должна появиться.
2. Двигайте ползунки — лицо модели должно меняться в реальном времени.
3. Для парных морфов проверьте, что ползунок Side корректно перераспределяет значения между L и R.
4. Нажмите **Randomize** — должно получиться случайное выражение лица.
5. Нажмите **Reset** — все морфы должны вернуться в ноль.
6. Сохраните пресет, перезапустите, загрузите — пресет должен восстановиться.
7. На втором клиенте убедитесь, что изменения морфов синхронизируются.


---

## ➡️ Следующий шаг

Переходи к **[21.03 — Пресеты хотбара (HotbarPresetsButton)](21_03_HotbarPresets.md)**.
