# 00.19 — Input (ввод): основы

## Что мы делаем?

Разбираем, как читать **нажатия клавиш, движение мыши и джойстика** в s&box. Это фундамент для Phase 17 («Система управления»), но базовое API нужно понимать **сразу** — иначе любой пример кода из дальнейших фаз непонятен.

## Класс `Input`

Весь ввод читается через статический класс `Input`. Вызывается **каждый кадр** в `OnUpdate`:

```csharp
public sealed class MyPlayer : Component
{
    protected override void OnUpdate()
    {
        if ( Input.Down( "jump" ) )
        {
            // прыжок удерживается
        }
    }
}
```

## Три способа спросить про клавишу

| Метод | Когда возвращает `true` |
|---|---|
| `Input.Down( "jump" )` | Пока клавиша **удерживается** (каждый кадр, пока нажата) |
| `Input.Pressed( "jump" )` | **В тот кадр**, когда её нажали (ровно 1 раз за нажатие) |
| `Input.Released( "jump" )` | **В тот кадр**, когда её отпустили |

Имена регистронезависимы: `"Jump"` == `"jump"`.

### Когда что?

- **`Pressed`** — однократные действия: выстрел одиночным, переключение оружия, открытие меню.
- **`Down`** — продолжительные: бег, прицеливание, удержание гранаты.
- **`Released`** — отпускание заряда: лук, граната с кукером.

## Аналоговый ввод

Для движения и мыши — не `bool`, а `Vector3`:

```csharp
Vector3 move = Input.AnalogMove;   // WSAD или стик (x=forward, y=right)
Vector3 look = Input.AnalogLook;   // дельта мыши или стик (pitch, yaw, roll)
```

`AnalogMove` нормализован: длина от 0 до 1. `AnalogLook` — дельты с прошлого кадра.

Пример движения:

```csharp
if ( !Input.AnalogMove.IsNearZeroLength )
{
    WorldPosition += Input.AnalogMove.Normal * 100f * Time.Delta;
}
```

## Кастомные клавиши в проекте

Имена клавиш (`"jump"`, `"attack1"`, `"use"`) — это **не хардкод**, а настройки проекта. Открываются в редакторе: **Project Settings → Input**.

Там видишь таблицу:

| Имя действия | Клавиша по умолчанию | Категория |
|---|---|---|
| `jump` | Space | Movement |
| `attack1` | Mouse1 | Combat |
| `use` | E | Interaction |
| ... | ... | ... |

Игрок в настройках игры может переназначить клавиши, но имя действия `"jump"` останется тем же — твой код продолжит работать.

Подробная настройка через файл `ProjectSettings/Input.config` — см. [00.06 — Настройки ввода](00_06_Настройки_ввода.md).

## Мышь

```csharp
Vector2 mouse      = Mouse.Position;     // пиксели от левого-верхнего угла экрана
Vector2 delta      = Mouse.Delta;        // движение за кадр
float wheel        = Input.MouseWheel.y; // прокрутка колеса
bool left          = Input.Down( "attack1" );   // обычно ЛКМ
bool visible       = Mouse.Visible;
```

Курсор прячут для FPS-управления и показывают для меню:

```csharp
Mouse.Visible = isMenuOpen;
```

## Клавиша Escape

По умолчанию Esc открывает пауз-меню движка. Если тебе надо своё меню:

```csharp
protected override void OnUpdate()
{
    if ( Input.EscapePressed )
    {
        Input.EscapePressed = false;    // «съесть» нажатие, чтобы движок его не увидел
        OpenMyMenu();
    }
}
```

## Инпут от сетевого игрока

⚠️ Важный момент для мультиплеера: **`Input` читается только на клиенте**. На хосте / в коде прокси не стоит его читать — там клавиш нет.

Типичный паттерн:

```csharp
protected override void OnUpdate()
{
    if ( IsProxy ) return;    // этим управляет кто-то другой
    ReadMyInput();
}
```

Что такое `IsProxy` — см. [00.22 — Ownership](00_22_Ownership.md).

## Глифы (иконки клавиш)

Для показа в UI правильных иконок (ЛКМ, Space, кнопка геймпада A) используется:

```csharp
Texture glyph = Input.GetGlyph( "jump" );
```

Возвращает текстуру, которую можно нарисовать в Razor или через HudPainter.

## Пример: полноценный ввод игрока

```csharp
protected override void OnFixedUpdate()
{
    if ( IsProxy ) return;

    // движение (XY)
    var wish = new Vector3(
        Input.AnalogMove.x,
        Input.AnalogMove.y,
        0 );

    // поворот камеры (уже применён к Look через OnUpdate)
    // здесь только перемещение
    Controller.Accelerate( wish * 200f );

    if ( Input.Pressed( "jump" ) && Controller.IsOnGround )
        Controller.Jump();
}
```

## Результат

После этого этапа ты знаешь:

- ✅ Как различать `Down`, `Pressed`, `Released`.
- ✅ Как читать мышь и аналоговый ввод.
- ✅ Что имена действий (`"jump"`) настраиваются в Project Settings.
- ✅ Что `Input` нельзя читать у прокси-объектов.
- ✅ Как перехватить Esc и показать свой курсор.

---

📚 **Facepunch docs:** [gameplay/input/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/gameplay/input/index.md)

**Следующий шаг:** [00.20 — Сеть: хост, клиент, соединение](00_20_Networking_Basics.md)
