# 00.28 — HudPainter (немедленный рендер HUD)

## Что мы делаем?

Разбираем **HudPainter** — альтернативу Razor для простых HUD-элементов. Это «нарисовать прямо сейчас, без панелей и стилей». В Sandbox он используется для мушки, индикаторов физгана, линий трассирования и прочей «рисовалки каждый кадр».

## Чем отличается от Razor

| HudPainter | Razor Panel |
|---|---|
| Рисуется каждый кадр вручную | Строится один раз, живёт |
| Нет стилей, лэйаута, событий | Полноценный UI-движок |
| Быстро, дёшево | Дороже |
| Для мушки, линий, простого текста | Для меню, инвентарей, чатов |

Правило: **если нужен интерактив (клики, ввод, hover) — Razor. Если только нарисовать — HudPainter.**

## Базовый пример

```csharp
public sealed class Crosshair : Component
{
    protected override void OnUpdate()
    {
        if ( Scene.Camera is null ) return;

        var hud = Scene.Camera.Hud;

        // Прямоугольник 10×10 в точке (300, 300)
        hud.DrawRect( new Rect( 300, 300, 10, 10 ), Color.White );
    }
}
```

Ключевая строка: `var hud = Scene.Camera.Hud;`. **Каждая камера имеет свой `HudPainter`**. Ты получаешь его и рисуешь.

## Основные методы

### Прямоугольники

```csharp
hud.DrawRect( new Rect( x, y, width, height ), Color.Red );

// Скруглённый
hud.DrawRect(
    new Rect( 100, 100, 200, 50 ),
    Color.Blue,
    new Vector4( 8, 8, 8, 8 )   // радиус углов TL,TR,BR,BL
);
```

### Линии

```csharp
hud.DrawLine(
    new Vector2( 100, 100 ),
    new Vector2( 200, 200 ),
    thickness: 2f,
    Color.White
);
```

### Круги и окружности

```csharp
hud.DrawCircle( center, radius, Color.Yellow );
```

### Текст

```csharp
hud.DrawText(
    new TextRendering.Scope( "Hello!", Color.Red, 32 ),
    new Vector2( Screen.Width * 0.5f, 50 )
);
```

`TextRendering.Scope` задаёт строку, цвет и размер. Можно менять шрифт, выравнивание, обводку.

### Текстуры

```csharp
hud.DrawTexture( new Rect( 10, 10, 128, 128 ), myTexture );
```

## Координаты — экранные пиксели

Все координаты **в пикселях**, от **левого-верхнего** угла экрана. Центр экрана:

```csharp
var center = new Vector2( Screen.Width * 0.5f, Screen.Height * 0.5f );
```

`Screen.Width` и `Screen.Height` — это размер окна в пикселях, пересчитывается каждый кадр.

## Пример: простая мушка

```csharp
public sealed class Crosshair : Component
{
    [Property] public float Size { get; set; } = 8f;
    [Property] public float Thickness { get; set; } = 2f;
    [Property] public Color LineColor { get; set; } = Color.White;

    protected override void OnUpdate()
    {
        if ( Scene.Camera is null ) return;
        var hud = Scene.Camera.Hud;

        var c = new Vector2( Screen.Width * 0.5f, Screen.Height * 0.5f );

        hud.DrawLine( c + Vector2.Left * Size,  c + Vector2.Right * Size, Thickness, LineColor );
        hud.DrawLine( c + Vector2.Up   * Size,  c + Vector2.Down  * Size, Thickness, LineColor );
    }
}
```

Это полноценная настраиваемая мушка — ~15 строк кода. Без CSS, без Razor.

## Пример: индикатор прогресса

```csharp
protected override void OnUpdate()
{
    if ( Scene.Camera is null || Progress <= 0 ) return;

    var hud = Scene.Camera.Hud;

    // Фон
    var rect = new Rect( 100, Screen.Height - 50, 300, 20 );
    hud.DrawRect( rect, Color.Black.WithAlpha( 0.6f ) );

    // Заполнение
    var fill = rect;
    fill.Width = rect.Width * Progress;
    hud.DrawRect( fill, Color.Orange );

    // Текст
    hud.DrawText(
        new TextRendering.Scope( $"{Progress:P0}", Color.White, 14 ),
        new Vector2( rect.Center.x, rect.Center.y )
    );
}
```

## Когда это использовать в Sandbox

В Sandbox через HudPainter часто делают:
- **Мушки оружия.**
- **Линии лазерного прицела.**
- **Трейсеры пуль.**
- **Индикаторы урона из-за экрана.**
- **Отладочную геометрию (дебаг NPC).**

Razor в это время держит: главное меню, инвентарь, чат, SpawnMenu, Scoreboard — то, где есть клики и структура.

## Важные ограничения

- ❌ **Нет событий мыши**. HudPainter — только рендер. Клики ловит Razor.
- ❌ **Нет авто-лэйаута**. Хочешь «по центру» — вычисляй вручную через `Screen.Width / 2`.
- ❌ **Нет анимаций автоматически**. Хочешь плавность — сам интерполируй значения.
- ✅ **Порядок рисования = порядок вызовов.** Что вызвал раньше, то ниже; поверх — что позже.

## Результат

После этого этапа ты знаешь:

- ✅ Что такое `HudPainter` и чем он отличается от Razor.
- ✅ Как получить `Scene.Camera.Hud` и рисовать каждый кадр.
- ✅ Базовые методы: `DrawRect`, `DrawLine`, `DrawCircle`, `DrawText`, `DrawTexture`.
- ✅ Систему координат HUD (от левого-верхнего угла, пиксели).
- ✅ Когда выбирать HudPainter, а когда Razor.

---

📚 **Facepunch docs:** [ui/hudpainter.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/ui/hudpainter.md)

**Следующий шаг:** [00.29 — GameResource (кастомные ресурсы)](00_29_GameResource.md)
