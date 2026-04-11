# Этап 05_06 — Nameplate (Табличка с именем)

## Что это такое?

Представьте бейджик на груди у сотрудника — вы сразу видите, как его зовут. **Nameplate** — это такой же «бейджик», который парит над головой каждого игрока в мире. Он показывает:

- 🖼️ Аватар из Steam
- 📛 Имя игрока
- 🔊 Индикатор голосового чата (если игрок говорит)

Важно: табличка видна только над **другими** игроками — над собой вы её не увидите.

---

## Файл `Nameplate.razor`

### Полный исходный код

**Путь:** `Code/UI/Nameplate.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent

@if (Player.IsValid() && !Player.IsLocalPlayer && !(Player.FindLocalPlayer()?.WantsHideHud ?? false))
{
	<root>

		<div class="card">

			<div class="avatar" style="background-image: url( avatar:@Player.SteamId )"></div>

			<label class="name">@Player.DisplayName</label>
			@if ( IsVoicePlaying )
			{
				<div class="voice">volume_up</div>
			}
		</div>

	</root>
}

@code
{
	[Property] public Player Player;

    Sandbox.Voice voiceComponent;

    protected override void OnEnabled()
    {
        base.OnEnabled();
        voiceComponent = GameObject.Root.Components.GetInDescendantsOrSelf<Voice>();
    }

    public bool IsVoicePlaying
    {
        get
        {
            if (voiceComponent is null) return false;
            return voiceComponent.LastPlayed < 0.5f;
        }
    }

    /// <summary>
    /// Rebuild if the owner connection changes
    /// </summary>
    protected override int BuildHash() => System.HashCode.Combine(Network.Owner, IsVoicePlaying, Player.FindLocalPlayer()?.WantsHideHud ?? false);
}
```

### Разбор кода

**Условие отображения:**

```razor
@if (Player.IsValid() && !Player.IsLocalPlayer && !(Player.FindLocalPlayer()?.WantsHideHud ?? false))
```

Табличка показывается **только** если:
1. Игрок существует (`Player.IsValid()`)
2. Это **не** вы (`!Player.IsLocalPlayer`)
3. HUD не скрыт

**Разметка:**

- `avatar:@Player.SteamId` — загружает аватар из Steam
- `@Player.DisplayName` — отображает имя игрока
- Если `IsVoicePlaying` — показывается иконка динамика (`volume_up` — это Material Icon)

**Код:**

| Элемент | Что делает |
|---------|-----------|
| `[Property] public Player Player` | Ссылка на игрока, над которым показывается табличка |
| `voiceComponent` | Компонент голосового чата этого игрока |
| `OnEnabled()` | При включении компонента — ищет компонент Voice в иерархии объекта |
| `IsVoicePlaying` | Возвращает `true`, если игрок говорил менее 0.5 секунды назад |
| `BuildHash()` | Перестраивает компонент при смене владельца, состояния голоса или видимости HUD |

---

## Файл `Nameplate.razor.scss`

### Полный исходный код

**Путь:** `Code/UI/Nameplate.razor.scss`

```scss
@import "/UI/Theme.scss";

Nameplate
{
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    white-space: nowrap;
    justify-content: center;
    align-items: center;
    font-weight: bold;

    .card
    {
        flex-direction: row;
        background-color: #0004;
        border-radius: 5px;
        padding: 10px 20px;
        justify-content: center;
        align-items: center;
        gap: 20px;
        font-size: 32px;

        .avatar
        {
            width: 64px;
            height: 64px;
            flex-shrink: 0;
            background-position: center;
            background-size: cover;
            border-radius: 50%;
        }

        .name
        {
            font-family: $body-font;
            color: #fff;
			text-overflow: ellipsis;
        }

        .voice
        {
            background-color: #111a;
            border-radius: 20px;
            padding: 4px;
            position: absolute;
            top: -12px;
            right: -12px;
            font-family: "Material Icons";
            font-size: 24px;
            color: #aaa;
        }
    }
}
```

### Разбор стилей

| Элемент | Описание |
|---------|----------|
| `Nameplate` | Занимает весь доступный блок, центрирует содержимое. `white-space: nowrap` — имя не переносится на новую строку |
| `.card` | Карточка: горизонтальная (`row`), полупрозрачный тёмный фон, скруглённые углы |
| `.avatar` | Круглая аватарка 64×64 пикселя. `border-radius: 50%` делает квадрат кругом |
| `.name` | Белый текст, при длинном имени обрезается с многоточием (`text-overflow: ellipsis`) |
| `.voice` | Иконка динамика — маленький кружок в правом верхнем углу карточки |

---

## Как выглядит в игре

```
         🔊
    ┌──────────────┐
    │  🟢  Игрок1  │    ← парит над головой другого игрока
    └──────────────┘
         🧑
        /|\
        / \
```

Табличка привязана к позиции игрока в 3D-мире и автоматически поворачивается к камере.
