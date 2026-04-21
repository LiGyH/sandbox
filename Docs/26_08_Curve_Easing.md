# 26.08 — Curve и интерполяция (Easing, Lerp)

## Что мы делаем?

Плавные движения, плавно нарастающие громкости, плавно затухающие эффекты — везде нужны **функции интерполяции**. Этот этап объясняет три инструмента: `Lerp`, `Easing` и `Curve` (редактируемая кривая в инспекторе).

## `Lerp` — линейная интерполяция

Самая простая «смесь» двух значений:

```csharp
float v = MathX.Lerp( from, to, t );          // t от 0 до 1
Vector3 p = Vector3.Lerp( a, b, 0.5f );       // ровно посередине
Color c = Color.Lerp( red, blue, t );
Rotation r = Rotation.Slerp( from, to, t );   // для углов — Slerp!
```

Главное правило: **для `Rotation` используй `Slerp`**, не `Lerp`. Slerp идёт по кратчайшей дуге, Lerp может «срезать» через центр сферы и выглядит странно.

## Простой бегущий лерп

```csharp
TimeSince _started = 0;
const float Duration = 1.5f;

protected override void OnUpdate()
{
    float t = MathX.Clamp( _started / Duration, 0f, 1f );
    GameObject.WorldPosition = Vector3.Lerp( startPos, endPos, t );
}
```

Это даст **равномерное** движение. Чтобы получить ускорение/замедление — добавь easing.

## `Easing` — нелинейные функции

`Easing` — статический класс с типичными «дугами». Вызов: `Easing.EaseOut( t )` — на вход 0..1, на выходе тоже 0..1, но кривая.

| Функция | Поведение |
|---|---|
| `EaseIn` | медленно стартует, ускоряется |
| `EaseOut` | быстро стартует, замедляется |
| `EaseInOut` | медленно, быстро, медленно |
| `BounceIn` / `BounceOut` | отскоки в начале/конце |
| `ElasticIn` / `ElasticOut` | пружинная анимация |
| `BackIn` / `BackOut` | сначала «оттягивается» назад |

```csharp
float eased = Easing.EaseOut( t );
GameObject.WorldPosition = Vector3.Lerp( startPos, endPos, eased );
```

В `Easing.Function` это делегат `float (float t)` — можно сохранить в поле/свойстве:

```csharp
[Property] public Easing.Function Curve { get; set; } = Easing.EaseInOut;
```

В инспекторе появится дропдаун со списком всех функций.

## `Curve` — кривая, редактируемая в инспекторе

`Curve` — это **встроенный редактор графика** для дизайнерской настройки без перекомпиляции. Поле объявляется так:

```csharp
[Property] public Curve DamageFalloff { get; set; }
```

В инспекторе ты увидишь окошко с осями X/Y и контрольными точками. Вычисление:

```csharp
float dmgMultiplier = DamageFalloff.Evaluate( distance / maxDistance );
float baseDamage = 100f;
float final = baseDamage * dmgMultiplier;
```

Хорошие применения `Curve`:

- **затухание урона по расстоянию** (близко 1.0, далеко 0.1);
- **громкость двигателя по скорости**;
- **прозрачность исчезающего трупа по времени**;
- **разброс пули по длительности удержания** (от 0 до 1 за полсекунды).

Дизайнер настраивает кривую визуально — вместо того чтобы заваливать тебя просьбами «измени формулу».

## `Gradient` — кривая для цвета

Аналог `Curve`, но возвращает `Color`. Удобно для:

- цвета лазера по «зарядке»;
- индикации здоровья (зелёный → жёлтый → красный);
- цвета света по времени суток.

```csharp
[Property] public Gradient HealthBarColor { get; set; }
Color c = HealthBarColor.Evaluate( health / maxHealth );
```

## `MathX.Approach` — «плавно подползти»

Когда нужно **постепенно догонять** меняющуюся цель (а не «выехать ровно за N секунд»), используй `Approach`:

```csharp
_currentSpeed = MathX.Approach( _currentSpeed, targetSpeed, step: 200 * Time.Delta );
```

Это устойчиво к скачкам цели и не зависит от старта анимации.

## `MathX.SmoothDamp`

Похоже на `Approach`, но **с инерцией** — поведение «как пружина без отскока». Используется для плавной камеры, преследующей игрока.

```csharp
_camPos = Vector3.SmoothDamp( _camPos, target, ref _vel, smoothTime: 0.15f, Time.Delta );
```

## Что важно запомнить

- `Lerp(a, b, t)` — линейный микс; для `Rotation` всегда `Slerp`.
- `Easing` — статические функции, оборачивающие `t` в S-/B-/I-кривые.
- `Curve`-поле даёт дизайнеру **визуальный редактор** в инспекторе — используй для всего, что должен крутить не программист.
- `Gradient` — то же, но для цвета.
- `Approach` / `SmoothDamp` — для следящих систем, когда цель сама меняется.

## Что дальше?

В [26.09](26_09_Color.md) — `Color` и цветовые пространства: чем отличается RGB от HSV, и почему для UI важен `Color.WithAlpha`.
