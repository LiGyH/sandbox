# Этап 05_07 — Voices (Индикатор голосового чата)

## Что это такое?

Представьте конференц-звонок: когда кто-то говорит, рядом с его именем загорается индикатор. **Voices** — это список игроков, которые прямо сейчас говорят в голосовой чат. Он появляется в правом нижнем углу экрана и показывает:

- 🖼️ Аватар говорящего
- 📛 Имя говорящего
- 💡 Свечение вокруг аватара, пульсирующее в такт громкости голоса

---

## Файл `Voices.razor`

### Полный исходный код

**Путь:** `Code/UI/Voices.razor`

```razor
@namespace Facepunch.UI
@inherits PanelComponent

<root>

    @foreach (var voice in VoiceList.Where( x => CanDisplay( x ) ) )
    {
        <div class="item-row">
            <div class="avatar-wrap">
                <div class="speaking-glow" style="opacity: @GetGlowOpacity( voice ); transform: scale( @GetGlowScale( voice ) )"></div>
                <img class="avatar" src="avatar:@voice.Network.Owner.SteamId" />
            </div>
            <label class="name">@voice.Network.Owner.DisplayName</label>
        </div>
    }

</root>

@code
{
    public IEnumerable<Voice> VoiceList => Scene.GetAllComponents<Voice>();

    private Dictionary<ulong, float> _smoothed = new();

    private float GetSmoothed( Voice voice )
    {
        var id = voice.Network.Owner.SteamId;
        _smoothed[id] = _smoothed.GetValueOrDefault( id, 0f ).LerpTo( voice.Amplitude, Time.Delta * 10f );
        return _smoothed[id];
    }

    private bool CanDisplay( Voice voice )
    {
        if ( voice.Network.Owner is null ) return false;
        return voice.LastPlayed < 0.25f;
    }

    private string GetGlowOpacity( Voice voice )
    {
        var s = GetSmoothed( voice );
        return Math.Clamp( s * 4f, 0.2f, 0.9f ).ToString( "0.##" );
    }

    private string GetGlowScale( Voice voice )
    {
        var s = GetSmoothed( voice );
        return Math.Clamp( 1f + s * 0.6f, 1f, 1.6f ).ToString( "0.##" );
    }

    protected override int BuildHash()
    {
        return HashCode.Combine( Time.Now );
    }
}
```

### Разбор кода

**Разметка:**

- `@foreach` — перебирает всех говорящих в данный момент
- `speaking-glow` — свечение вокруг аватара. Его прозрачность и размер зависят от громкости голоса
- `avatar:@voice.Network.Owner.SteamId` — аватар игрока из Steam
- `@voice.Network.Owner.DisplayName` — имя игрока

**Код:**

| Элемент | Что делает |
|---------|-----------|
| `VoiceList` | Получает все компоненты Voice в сцене (каждый игрок имеет такой компонент) |
| `_smoothed` | Словарь для сглаживания громкости. Без сглаживания свечение бы мигало слишком резко |
| `GetSmoothed()` | Плавно интерполирует текущую громкость (`LerpTo`). `Time.Delta * 10f` — скорость сглаживания |
| `CanDisplay()` | Показывать игрока, только если он говорил менее 0.25 секунды назад |
| `GetGlowOpacity()` | Прозрачность свечения: от 0.2 (минимум) до 0.9 (максимум), зависит от громкости |
| `GetGlowScale()` | Размер свечения: от 1.0 (тихо) до 1.6 (громко) |
| `BuildHash()` | Обновляется каждый кадр (привязан к `Time.Now`) |

> **Для новичков:** `LerpTo` — это «линейная интерполяция». Представьте, что вы плавно двигаете ползунок от текущего значения к новому, вместо того чтобы резко перескочить. Это делает анимацию плавной.

---

## Файл `Voices.razor.scss`

### Полный исходный код

**Путь:** `Code/UI/Voices.razor.scss`

```scss
@import "/UI/Theme.scss";

Voices
{
    position: absolute;
    right: 128px;
    bottom: 256px;
    gap: 6px;
    flex-direction: column;
    align-items: flex-end;

    .item-row
    {
        gap: 10px;
        align-items: center;
        padding: 8px 14px 8px 12px;
        justify-content: flex-end;
        background-color: $hud-element-bg;
        border-radius: $hud-radius-sm;
        overflow: hidden;
    }

    .avatar-wrap
    {
        position: relative;
        width: $hud-icon-size;
        height: $hud-icon-size;
        flex-shrink: 0;
    }

    .speaking-glow
    {
        position: absolute;
        width: 56px;
        height: 56px;
        left: -13px;
        top: -13px;
        border-radius: 100%;
        background: radial-gradient( rgba(0, 148, 255, 0.7) 0%, transparent 70% );
    }

    img
    {
        width: $hud-icon-size;
        height: $hud-icon-size;
        border-radius: 100px;
    }

    label
    {
        font-family: $title-font;
        color: $hud-text;
        font-size: 15px;
        font-weight: 600;
        text-shadow: 0px 0px 8px $color-accent;
    }
}
```

### Разбор стилей

| Элемент | Описание |
|---------|----------|
| `Voices` | Расположен справа внизу (`right: 128px; bottom: 256px`), столбцом, выровнен вправо |
| `.item-row` | Строка говорящего: полупрозрачный тёмный фон, скруглённые углы |
| `.avatar-wrap` | Обёртка для аватара — задаёт размер и позиционирование свечения |
| `.speaking-glow` | Голубое радиальное свечение вокруг аватара. Размер 56×56 (больше аватара), центрировано отрицательными отступами |
| `img` | Круглый аватар размером 30×30 (значение `$hud-icon-size`) |
| `label` | Имя игрока: шрифт Poppins, белый цвет, с голубой тенью |

---

## Как выглядит на экране

```
┌──────────────────────────────────────────────┐
│                                              │
│                                              │
│                                              │
│                            ┌──────────────┐  │
│                            │ 🟢 Игрок1    │  │
│                            │ 🔵 Игрок3    │  │
│                            └──────────────┘  │
│  ❤️ 100                              🔫 30   │
└──────────────────────────────────────────────┘
```

Свечение вокруг аватара пульсирует: чем громче игрок говорит, тем ярче и больше свечение.
