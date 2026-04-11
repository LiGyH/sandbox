# Этап 05_09 — PressableHud (Интерактивные объекты и подсказки)

## Что это такое?

Представьте, что вы подходите к двери в игре и на экране появляется подсказка: «🚪 Дверь — Нажмите E, чтобы открыть». **PressableHud** — это система, которая показывает такие подсказки. Когда игрок смотрит на интерактивный объект (кнопку, дверь, предмет), в центре экрана появляется:

- 🔘 Прицел (точка), который становится активным
- 📋 Всплывающая подсказка с иконкой, названием и описанием объекта

Компонент состоит из двух частей:
- **PressableHud** — прицел и логика определения, на что смотрит игрок
- **PressableTooltip** — всплывающая подсказка с информацией

---

## Файл `PressableHud.razor`

### Полный исходный код

**Путь:** `Code/UI/Pressable/PressableHud.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
@namespace Sandbox

<root>

    <div class="crosshair">
    </div>   

</root>

@code
{
    int _hashCode;
    PressableTooltip _tooltip;

    protected override void OnUpdate()
    {
        base.OnUpdate();

        var tt = GetHovered();
        var hc = HashCode.Combine(tt.Title, tt.Icon, tt.Description);

        if (hc != _hashCode)
        {
            _hashCode = hc;
            _tooltip?.Delete();
            _tooltip = null;

            if (!string.IsNullOrWhiteSpace(tt.Title) || !string.IsNullOrWhiteSpace(tt.Icon) || !string.IsNullOrWhiteSpace(tt.Description))
            {
                _tooltip = Panel.AddChild<PressableTooltip>();
                _tooltip.Title = tt.Title;
                _tooltip.Icon = tt.Icon;
                _tooltip.Description = tt.Description;
            }

        }

        SetClass("active", tt.Pressable != null && tt.Enabled);
    }

    IPressable.Tooltip GetHovered()
    {
        var lp = Player.FindLocalPlayer();
        if (lp is null) return default;
        if (lp.Controller.Tooltips.Count == 0) return default;

        return lp.Controller.Tooltips.FirstOrDefault();
    }
}
```

### Разбор кода

**Разметка:**

```razor
<div class="crosshair">
</div>
```
Простой прицел (точка) в центре экрана. Его видимость управляется через CSS-класс `active`.

**Код:**

| Элемент | Что делает |
|---------|-----------|
| `_hashCode` | Хэш-код текущей подсказки — используется для оптимизации (не пересоздавать, если ничего не изменилось) |
| `_tooltip` | Текущая всплывающая подсказка (или `null`, если нет) |
| `OnUpdate()` | Каждый кадр проверяет, на что смотрит игрок |
| `GetHovered()` | Получает информацию об объекте, на который смотрит игрок |

**Логика OnUpdate():**

1. Получить подсказку от объекта, на который смотрит игрок (`GetHovered()`)
2. Вычислить хэш-код подсказки
3. Если хэш изменился (игрок посмотрел на другой объект):
   - Удалить старую подсказку
   - Если есть новая информация — создать новую подсказку `PressableTooltip`
4. Если есть активный интерактивный объект — добавить класс `active` (прицел становится видимым)

---

## Файл `PressableHud.razor.scss`

### Полный исходный код

**Путь:** `Code/UI/Pressable/PressableHud.razor.scss`

```scss
@import "/UI/Theme.scss";

PressableHud
{
	position: absolute;
	top: 0;
	left: 0;
	right: 0;
	bottom: 0;
	justify-content: center;
	align-items: center;
	font-family: $body-font;
	font-size: 14px;
	font-weight: 600;
	z-index: 1000;
	opacity: 0.2;

	.crosshair
	{
		width: 12px;
		height: 12px;
		background-color: white;
		border-radius: 50%;
		box-shadow: 0px 0px 8px black;
		opacity: 0;
		transition: all 0.1s linear;
	}

	&.active
	{
		opacity: 1;

		.crosshair
		{
			opacity: 1;
		}
	}
}
```

### Разбор стилей

| Элемент | Описание |
|---------|----------|
| `PressableHud` | Занимает весь экран, содержимое центрировано. `z-index: 1000` — поверх остальных элементов |
| `.crosshair` | Белая точка 12×12 с тенью. По умолчанию невидима (`opacity: 0`) |
| `&.active` | Когда игрок смотрит на интерактивный объект — точка и весь блок становятся видимыми |
| `transition: all 0.1s linear` | Плавное появление/исчезновение за 0.1 секунды |

---

## Файл `PressableTooltip.razor`

Всплывающая подсказка, которая появляется рядом с прицелом.

### Полный исходный код

**Путь:** `Code/UI/Pressable/PressableTooltip.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root class="tt">

    <div class="icon">@Icon</div>
    <div class="title">@Title</div>
    <div class="description">@Description</div>

</root>

@code
{
    [Parameter] public string Title { get; set; }
    [Parameter] public string Icon { get; set; }
    [Parameter] public string Description { get; set; }
}
```

### Разбор кода

Очень простой компонент — принимает три параметра и отображает их:

| Параметр | Что показывает |
|----------|---------------|
| `Icon` | Иконка (Material Icon) — например, 🚪 для двери |
| `Title` | Название объекта — например, «Дверь» |
| `Description` | Описание действия — например, «Нажмите E» |

Атрибут `@inherits Panel` (а не `PanelComponent`) означает, что это дочерний элемент, а не самостоятельный компонент сцены.

---

## Файл `PressableTooltip.razor.scss`

### Полный исходный код

**Путь:** `Code/UI/Pressable/PressableTooltip.razor.scss`

```scss
@import "/UI/Theme.scss";

.tt
{
	position: absolute;
	width: 512px;
	height: 512px;
	flex-direction: column;
	font-family: $body-font;
	font-weight: 700;
	font-size: 1.5rem;
	color: #fff;
	text-shadow: 1px 1px 4px black;

	.icon
	{
		align-items: flex-end;
		justify-content: center;
		flex-shrink: 0;
		flex-grow: 0;
		font-size: 4rem;
		font-family: "Material Icons";
		height: 180px;
	}

	.title
	{
		flex-shrink: 0;
		flex-grow: 0;
		align-items: flex-end;
		justify-content: center;
		font-size: 1.5rem;
		margin-bottom: 80px;
	}

	.description
	{
		margin-top: 20px;
		flex-grow: 1;
		align-items: flex-start;
		justify-content: center;
		flex-shrink: 0;
		font-weight: 600;
		font-size: 1rem;
	}

	transition: all 0.1s ease;

	&:intro
	{
		transform: scale( 0.2 );
		opacity: 0;
	}

	&:outro
	{
		transform: scale( 0.9 );
		opacity: 0;
	}
}
```

### Разбор стилей

| Элемент | Описание |
|---------|----------|
| `.tt` | Контейнер подсказки: 512×512, вертикальная колонка, белый текст с чёрной тенью |
| `.icon` | Большая иконка вверху (4rem ≈ 64px), шрифт Material Icons |
| `.title` | Название объекта под иконкой, с большим отступом снизу (80px) |
| `.description` | Описание действия — шрифт поменьше (1rem) |
| `&:intro` | Анимация появления: подсказка «вырастает» из маленького размера (scale 0.2 → 1.0) |
| `&:outro` | Анимация исчезновения: подсказка слегка уменьшается и растворяется |

---

## Как выглядит на экране

Когда игрок **не** смотрит на объект:
```
┌──────────────────────────────────────────────┐
│                                              │
│                                              │
│                      (ничего)                │
│                                              │
│                                              │
└──────────────────────────────────────────────┘
```

Когда игрок **смотрит** на интерактивный объект:
```
┌──────────────────────────────────────────────┐
│                                              │
│                      🚪                      │
│                     Дверь                     │
│                      ●                       │
│                 Нажмите E                     │
│                                              │
└──────────────────────────────────────────────┘
```

Подсказка плавно появляется при наведении и плавно исчезает при отведении взгляда.
