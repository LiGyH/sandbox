# Этап 09_09 — ToolInfoPanel (Панель информации об инструменте)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Создаём интерфейс `IToolInfo` и UI-компонент `ToolInfoPanel` — информационную панель, которая появляется в верхнем левом углу экрана, когда игрок держит инструмент. Она показывает название инструмента, описание и подсказки по клавишам управления.

## Зачем это нужно?

Представьте табличку-подсказку на станке в мастерской: «Дрель — для сверления отверстий. ЛКМ — сверлить. ПКМ — извлечь сверло. R — сменить насадку». Именно так работает `ToolInfoPanel` — когда вы берёте инструмент, появляется панель с:

- **Названием** инструмента
- **Описанием** — что он делает
- **Подсказками действий** — какие клавиши за что отвечают (ЛКМ, ПКМ, R)

Панель плавно появляется и исчезает при смене оружия.

## Как это работает внутри движка?

### Архитектура

```
IToolInfo (интерфейс)
  ├── Name         — название инструмента
  ├── Description  — описание
  ├── PrimaryAction   — подсказка для ЛКМ (attack1)
  ├── SecondaryAction  — подсказка для ПКМ (attack2)
  └── ReloadAction     — подсказка для R (reload)

ToolInfoPanel (PanelComponent)
  └── Ищет IToolInfo у активного оружия и отображает его данные
```

- **`IToolInfo`** — контракт: любой компонент/оружие может реализовать его и показать свою информацию.
- **`ToolInfoPanel`** — Razor-компонент, который ищет `IToolInfo` у текущего оружия игрока.
- **`CurrentToolInfo`** — сначала ищет `IToolInfo` среди дочерних компонентов оружия (например, `ToolMode`), затем проверяет само оружие.
- Панель видна (`visible`) только когда есть хотя бы одно действие.

## Создай файл

📁 `Code/UI/ToolInfo/IToolInfo.cs`

```csharp
﻿namespace Sandbox;

public interface IToolInfo : IValid
{
	string Name { get; }
	string Description { get; }
	string PrimaryAction => null;
	string SecondaryAction => null;
	string ReloadAction => null;
}
```

### Разбор IToolInfo

- **`: IValid`** — интерфейс поддерживает проверку `IsValid`, что важно для компонентов движка, которые могут быть уничтожены.
- **Свойства по умолчанию** — `PrimaryAction`, `SecondaryAction` и `ReloadAction` возвращают `null` по умолчанию. Инструмент может переопределить только нужные действия.
- **Пример**: `ToolMode` (базовый класс инструментов) реализует `IToolInfo` и задаёт `Name`, `Description` и действия для каждого конкретного инструмента.

---

📁 `Code/UI/ToolInfo/ToolInfoPanel.razor`

```razor
﻿@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

@if ( Player.IsValid() && Player.WantsHideHud )
    return;

@if ( !HasAnyAction )
    return;

<root>
 <div class="panel">

  <div class="name">@Info?.Name</div>

  @if ( !string.IsNullOrEmpty( Info?.Description ) )
  {
   <div class="description">@Info.Description</div>
  }

  <div class="divider"></div>

        <div class="actions">
            @if (!string.IsNullOrEmpty(Info?.PrimaryAction))
            {
                <div class="action">
                    <InputHint Action="attack1" class="key" />
                    <span class="text">@Info.PrimaryAction</span>
                </div>
            }
            @if (!string.IsNullOrEmpty(Info?.SecondaryAction))
            {
                <div class="action">
                    <InputHint Action="attack2" class="key" />
                    <span class="text">@Info.SecondaryAction</span>
                </div>
            }
            @if (!string.IsNullOrEmpty(Info?.ReloadAction))
            {
                <div class="action">
                    <InputHint Action="reload" class="key" />
                    <span class="text">@Info.ReloadAction</span>
                </div>
            }
        </div>
 </div>
</root>

@code
{
	IToolInfo CurrentToolInfo
	{
		get
		{
			var inv = Player?.GetComponent<PlayerInventory>();
			
			if ( !inv.IsValid() || !inv.ActiveWeapon.IsValid() )
				return null;
			
			if ( inv.ActiveWeapon.GetComponentInChildren<IToolInfo>() is { IsValid: true } toolInfo )
				return toolInfo;

			return inv?.ActiveWeapon as IToolInfo;
		}
	}

	Player Player => Player.FindLocalPlayer();

	IToolInfo Info => CurrentToolInfo;

	bool HasAnyAction => !string.IsNullOrEmpty( Info?.PrimaryAction )
	                     || !string.IsNullOrEmpty( Info?.SecondaryAction )
	                     || !string.IsNullOrEmpty( Info?.ReloadAction );

	protected override void OnUpdate()
	{
		SetClass( "visible", CurrentToolInfo is not null );
	}

	protected override int BuildHash() => System.HashCode.Combine( Info?.Name, Info?.Description, Info?.PrimaryAction, Info?.SecondaryAction, Info?.ReloadAction, Player?.WantsHideHud );
}
```

### Разбор ключевых частей

- **`CurrentToolInfo`** — свойство с двухуровневым поиском:
  1. Сначала ищет `IToolInfo` среди дочерних компонентов активного оружия (`GetComponentInChildren<IToolInfo>()`). Это нужно для Toolgun, у которого `ToolMode` — дочерний компонент.
  2. Если не нашёл — проверяет, реализует ли само оружие `IToolInfo` (`as IToolInfo`).
- **`HasAnyAction`** — панель показывается только если есть хотя бы одно действие. Если инструмент не задал ни одного — панель скрыта.
- **`InputHint`** — встроенный компонент, показывающий иконку клавиши для указанного действия (например, ЛКМ для `attack1`).
- **`BuildHash()`** — хеш из всех отображаемых данных. Razor пересобирает UI только при изменении.
- **`@namespace Sandbox`** — компонент находится в пространстве имён `Sandbox`, чтобы быть доступным другим частям проекта.

---

📁 `Code/UI/ToolInfo/ToolInfoPanel.razor.scss`

```scss
@import "/UI/Theme.scss";

ToolInfoPanel
{
	position: absolute;
	left: 2vw;
	top: 2vw;
	justify-content: flex-start;
	pointer-events: none;
	opacity: 0;
	transition: opacity 0.15s ease;

	&.visible
	{
		opacity: 1;
	}

	.panel
	{
		flex-direction: column;
		align-items: flex-start;
		gap: 4px;
		font-family: $title-font;
		padding: 16px;
		background-color: $hud-element-bg;
		border-radius: $hud-radius;
	}

	.actions
	{
		gap: 16px;
	}

	.name
	{
		font-size: 32px;
		font-weight: 700;
		color: $color-accent;
		margin-bottom: 2px;
	}

	.description
	{
		font-size: 16px;
		font-weight: 400;
		font-family: $subtitle-font;
		color: $hud-text;
		margin-bottom: 4px;
	}

	.divider
	{
		width: 100%;
		height: 1px;
		background-color: rgba($color-accent, 0.15);
		margin-bottom: 4px;
	}

	.action
	{
		flex-direction: row;
		align-items: center;
		gap: 10px;
	}

	.key
	{
		width: 48px;
	}

	.text
	{
		font-size: 16px;
		font-weight: 500;
		color: $hud-text;
	}
}
```

### Разбор стилей

- **Позиция** — верхний левый угол (`left: 2vw; top: 2vw`), не перехватывает клики.
- **Плавное появление** — `opacity: 0` по умолчанию, при наличии инструмента добавляется класс `visible` → `opacity: 1` с переходом 0.15с.
- **`.panel`** — фон `$hud-element-bg`, скруглённые углы, вертикальная компоновка.
- **`.name`** — крупный заголовок (32px, жирный) цвета акцента.
- **`.description`** — описание мельче (16px), другой шрифт (`$subtitle-font`).
- **`.divider`** — тонкая горизонтальная линия, отделяющая описание от действий.
- **`.action`** — строка: иконка клавиши (48px) + текст действия.


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[10.01 — 🔗 BaseConstraintToolMode.cs — Базовый класс инструментов-соединений](10_01_BaseConstraint.md)** — начало следующей фазы.
