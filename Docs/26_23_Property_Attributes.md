# 26.23 — Property Attributes: полный справочник

## Что мы делаем?

В [00.13](00_13_Component_Attributes.md) мы рассмотрели базовые атрибуты — `[Property]`, `[Sync]`, `[Button]`. Здесь — **расширенный справочник** атрибутов из официальной документации, которые форматируют, валидируют и группируют поля компонентов в инспекторе.

## Группа 1: видимость и базовые

| Атрибут | Что делает |
|---|---|
| `[Property]` | поле/свойство видно в инспекторе |
| `[Hide]` | спрятать из инспектора (даже если `[Property]` есть) |
| `[ReadOnly]` | показать, но не давать редактировать |
| `[ShowIf( "OtherField", value )]` | показать только если другое поле = значение |
| `[HideIf( "OtherField", value )]` | спрятать только если условие |

```csharp
[Property] public bool UseRadius { get; set; }
[Property, ShowIf( nameof(UseRadius), true )] public float Radius { get; set; } = 32f;
```

## Группа 2: группировка

| Атрибут | Что делает |
|---|---|
| `[Group( "Damage" )]` | сложить все поля под общим заголовком в инспекторе |
| `[Category( "Visual" )]` | вкладки/категории |
| `[Order( 5 )]` | порядок вывода (меньше — выше) |
| `[Title( "Урон в секунду" )]` | переопределить отображаемое имя |

```csharp
[Group( "Damage" ), Property] public int BaseDamage { get; set; }
[Group( "Damage" ), Property] public float CritMultiplier { get; set; }

[Group( "Visual" ), Property] public Color MuzzleColor { get; set; }
```

## Группа 3: ограничение значений

| Атрибут | Что делает |
|---|---|
| `[Range( min, max )]` | ползунок с диапазоном |
| `[Range( min, max, step )]` | ползунок с шагом |
| `[MinMax( min, max )]` | для пары значений (диапазон) |
| `[Step( 0.1 )]` | дискретность ввода |

```csharp
[Property, Range( 0, 100 )] public int Health { get; set; } = 100;
[Property, Range( 0f, 1f, 0.05f )] public float Volume { get; set; } = 1f;
```

## Группа 4: подсказки и описания

| Атрибут | Что делает |
|---|---|
| `[Description( "..." )]` | подсказка при наведении |
| `[InputHint( "..." )]` | подсказка внутри пустого поля |
| `[Icon( "smile" )]` | иконка рядом с именем |

```csharp
[Property, Description( "Сколько секунд между выстрелами" )]
public float FireRate { get; set; } = 0.1f;
```

## Группа 5: ссылки и фильтры

| Атрибут | Что делает |
|---|---|
| `[ResourceType( "weapon" )]` | принимать только ассеты этого типа |
| `[Tags( "world", "solid" )]` | подсказывать только эти теги в выпадашке |
| `[InputAction]` | поле — имя input-action из настроек |
| `[FilePath( ext: "png" )]` | поле — путь к файлу нужного расширения |

```csharp
[Property, ResourceType( "weapon" )] public WeaponResource Default { get; set; }
[Property, InputAction] public string FireAction { get; set; } = "attack1";
```

## Группа 6: сетевая синхронизация

| Атрибут | Что делает |
|---|---|
| `[Sync]` | значение реплицируется владельцем всем клиентам |
| `[Sync( SyncFlags.FromHost )]` | только хост может менять |
| `[HostSync]` | синоним «менять может только хост» |
| `[Change( nameof(Method) )]` | вызвать метод, когда значение пришло по сети |

```csharp
[Sync, Change( nameof(OnHealthChanged) )]
public int Health { get; set; }

void OnHealthChanged( int oldVal, int newVal ) { Log.Info( $"HP: {oldVal} → {newVal}" ); }
```

См. также [00.24](00_24_Sync_Properties.md).

## Группа 7: события и кнопки

| Атрибут | Что делает |
|---|---|
| `[Button( "Подпись" )]` | кнопка в инспекторе, вызывает метод |
| `[Button, ConfirmDialog( "Уверен?" )]` | с подтверждением |

```csharp
[Button( "Сбросить здоровье" )]
void ResetHealth() { Health = MaxHealth; }
```

## Группа 8: жизненный цикл редактора

| Атрибут | Что делает |
|---|---|
| `[ExecuteInEditMode]` | компонент работает и в режиме редактирования (не только в Play) |
| `[ComponentEditorWidget( typeof(...) )]` | свой Widget для отображения |
| `[Icon( "..." ), Title( "..." )]` | как компонент выглядит в списке |

## Группа 9: коллекции

| Атрибут | Что делает |
|---|---|
| `[Property] List<...>` | редактируемый список (плюсик/минусик в инспекторе) |
| `[InlineEditor]` | редактировать вложенный объект прямо здесь, без перехода |

```csharp
[Property] public List<WeaponSlot> Slots { get; set; } = new();

[Serializable, InlineEditor]
public class WeaponSlot
{
    [Property] public string Name { get; set; }
    [Property] public WeaponResource Weapon { get; set; }
}
```

## Лучшие практики

1. **Группируй связанные поля** через `[Group]` — иначе у длинного компонента простыня из 50 полей.
2. **Давай дефолты.** `= 100`, `= "default"`. Дизайнеру не надо гадать, чем заполнить.
3. **Подписывай кнопки человеческим языком**, не `DoStuff`.
4. **Используй `Range` вместо «введите 0-1»** — ползунок физически невозможно сломать.
5. **`[Title]` перекрывает имя поля**, но **переименование поля ломает сериализацию**. Лучше переименовать через `[Title]`, а имя оставить.
6. **`ExecuteInEditMode` — осторожно.** Компонент будет жить в редакторе → следи, чтобы он не дёргал сетевой код.

## Что важно запомнить

- `[Property]` + `[Group]` + `[Range]` + `[Description]` — основной набор «чтобы не было больно дизайнеру».
- `[Sync]` + `[Change]` для сетевой репликации с обработчиком.
- `[ResourceType]` / `[Tags]` / `[InputAction]` сужают выпадающие списки до релевантных значений.
- `[Button]` превращает обычный метод в кнопку инспектора.
- `[InlineEditor]` редактирует вложенный объект прямо в форме.
- Не переименовывай поля — переименовывай через `[Title]`.

## Что дальше?

В [26.24](26_24_Services.md) — встроенные сервисы s&box: достижения, лидерборды, статистика.
