# 🖼️ ToolInfoPanel — UI-панель информации об инструменте

## Что мы делаем?

Создаём **три файла** для UI-панели, которая отображает в левом верхнем углу экрана информацию о текущем инструменте:
1. `IToolInfo.cs` — интерфейс с описанием инструмента
2. `ToolInfoPanel.razor` — Razor-компонент панели
3. `ToolInfoPanel.razor.scss` — стили панели

## Зачем это нужно?

Когда игрок экипирует тулган и выбирает инструмент, ему нужно видеть:
- **Название** инструмента (например, «Weld»)
- **Описание** (например, «Соединяет два объекта»)
- **Подсказки клавиш** (ЛКМ = «Выбрать точку», ПКМ = «Отмена», R = «Удалить»)

Эта панель появляется автоматически, когда активное оружие реализует `IToolInfo`.

## Как это работает внутри движка?

### Интерфейс IToolInfo

```
IToolInfo (интерфейс)
  ├── Name           — название инструмента
  ├── Description    — описание
  ├── PrimaryAction  — подсказка для ЛКМ (или null)
  ├── SecondaryAction— подсказка для ПКМ (или null)
  └── ReloadAction   — подсказка для R (или null)
```

`ToolMode` реализует `IToolInfo` (см. 09_01). Это значит, что **каждый инструмент** автоматически предоставляет данные для этой панели.

### Логика отображения

```
ToolInfoPanel.OnUpdate()
  └── CurrentToolInfo — ищет IToolInfo
        ├── PlayerInventory.ActiveWeapon
        │     └── GetComponentInChildren<IToolInfo>()  — ищет ToolMode
        └── ActiveWeapon as IToolInfo — или само оружие
  
HasAnyAction — панель показывается только если есть хотя бы одна подсказка
visible CSS-класс — управляет opacity (плавное появление/исчезновение)
```

### BuildHash

```csharp
protected override int BuildHash() => System.HashCode.Combine(
    Info?.Name, Info?.Description,
    Info?.PrimaryAction, Info?.SecondaryAction, Info?.ReloadAction,
    Player?.WantsHideHud
);
```

`BuildHash` — оптимизация рендеринга: панель перерисовывается только когда данные **действительно** изменились.

## Создай файл №1

📄 `Code/UI/ToolInfo/IToolInfo.cs`

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

## Создай файл №2

📄 `Code/UI/ToolInfo/ToolInfoPanel.razor`

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

## Создай файл №3

📄 `Code/UI/ToolInfo/ToolInfoPanel.razor.scss`

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

## Как добавить на HUD?

Эта панель — `PanelComponent`. Чтобы она работала, нужно добавить `ToolInfoPanel` на HUD-объект в сцене (обычно на тот же `GameObject`, где находится главная HUD-панель).

## Что проверить?

1. Экипируйте тулган
2. В левом верхнем углу должна появиться панель с названием инструмента и подсказками клавиш
3. При переключении инструмента панель должна обновляться
4. Если нет подсказок — панель скрыта

---

✅ **Фаза 9 завершена!** Теперь у вас есть полная система ToolGun:
- Базовый класс `ToolMode` с cookies, эффектами, трассировкой и snap grid
- Оружие `Toolgun` с автоматическим созданием и переключением инструментов
- Интерфейс `IToolgunEvent` для контроля доступа
- UI-панель `ToolInfoPanel` для отображения подсказок

➡️ Переходим к Фазе 10: [10_01 — BaseConstraintToolMode.cs](10_01_BaseConstraint.md)
