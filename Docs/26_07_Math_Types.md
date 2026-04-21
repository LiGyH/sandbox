# 26.07 — Математические типы: Vector3, Rotation, Angles

## Что мы делаем?

Каждый этап этого руководства использует `Vector3`, `Rotation`, `Angles`. Мы их применяли «по контексту», но никогда не разбирали системно. Этот файл — **справочник по основным математическим типам s&box** и ответ на классический вопрос: *«в чём разница между `Rotation` и `Angles`?»*

## `Vector3` — точка/направление в 3D

Просто три числа: `X`, `Y`, `Z`.

```csharp
var v = new Vector3( 10, 20, 30 );
v.X = 50;
float length = v.Length;            // длина (норма)
float lengthSqr = v.LengthSquared;  // длина в квадрате (быстрее, без sqrt)
var normalized = v.Normal;          // единичный вектор того же направления
var distance = Vector3.DistanceBetween( a, b );
var dot = a.Dot( b );               // скалярное произведение
var cross = a.Cross( b );           // векторное произведение
```

Есть готовые константы:

| Имя | Значение | Что значит в s&box (Source 2) |
|---|---|---|
| `Vector3.Zero` | (0,0,0) | ноль |
| `Vector3.One` | (1,1,1) | единичный |
| `Vector3.Up` | (0,0,1) | вверх (Z вверх!) |
| `Vector3.Down` | (0,0,-1) | вниз |
| `Vector3.Forward` | (1,0,0) | вперёд (X вперёд!) |
| `Vector3.Backward` | (-1,0,0) | назад |
| `Vector3.Left` | (0,1,0) | влево (Y влево!) |
| `Vector3.Right` | (0,-1,0) | вправо |

> ⚠️ **Источник частых ошибок:** в s&box (как и в Source) ось **Z — вертикаль**, не Y. У игроков из Unity/Unreal первое время ломается интуиция.

## `Vector2` и `Vector4`

Аналогично, для UI (`Vector2` — пиксели/проценты экрана) и шейдеров (`Vector4` — RGBA, кватернионы и т.п.).

## `Rotation` — кватернион

`Rotation` — это **кватернион** (`X, Y, Z, W`), внутреннее представление поворота. Он не страдает от *gimbal lock* и хорошо интерполируется. Создавать его руками не нужно — есть фабрики:

```csharp
Rotation r = Rotation.Identity;                 // нулевой поворот
r = Rotation.From( pitch: 0, yaw: 90, roll: 0 );
r = Rotation.From( new Angles( 0, 90, 0 ) );    // то же самое
r = Rotation.LookAt( direction );               // смотреть в направлении
r = Rotation.LookAt( target - origin, Vector3.Up );

// Извлечь оси:
Vector3 fwd = r.Forward;
Vector3 up  = r.Up;
Vector3 rt  = r.Right;

// Применить вращение к вектору:
Vector3 rotated = r * Vector3.Forward;

// Скомбинировать два поворота:
Rotation combined = r1 * r2;

// Плавная интерполяция:
Rotation mid = Rotation.Slerp( a, b, 0.5f );
```

## `Angles` — тройка эйлеровых углов

`Angles` — человеко-читаемая запись: `Pitch, Yaw, Roll`. Используется в инспекторе и в коде, когда нужны **углы в градусах**:

| Поле | Ось вращения | Что делает |
|---|---|---|
| `Pitch` | вокруг Y | смотрит вверх/вниз |
| `Yaw` | вокруг Z | поворот налево/направо |
| `Roll` | вокруг X | наклон вбок (как самолёт) |

```csharp
var a = new Angles( pitch: 0, yaw: 90, roll: 0 );
Rotation r = a.ToRotation();    // или a (есть неявное приведение)
Angles back = r.Angles();
```

> 🧠 **Когда что использовать:** `Angles` — для ввода (мышь, инспектор), `Rotation` — для математики, хранения, интерполяции. Не «крути» через `Angles += ...`, можно потерять ось.

## `Transform`

Тройка «позиция + поворот + масштаб»:

```csharp
var t = new Transform( pos, rot, scale );

// Превратить локальные координаты в мировые:
Vector3 worldPoint = t.PointToWorld( new Vector3( 0, 0, 64 ) );

// Превратить мировые координаты в локальные:
Vector3 localPoint = t.PointToLocal( worldPoint );
```

Используется на каждом `GameObject`: `GameObject.WorldTransform`, `GameObject.LocalTransform`.

## `BBox` — Axis-Aligned Bounding Box

Прямоугольный объём, выровненный по осям мира:

```csharp
var b = new BBox( -16, 16 );             // куб 32×32×32 вокруг центра
var b2 = new BBox( Vector3.Zero, new Vector3( 64, 64, 72 ) );

bool inside = b.Contains( point );
bool overlap = b.Overlaps( other );
Vector3 center = b.Center;
Vector3 size = b.Size;
b = b.AddPoint( newPoint );              // расширить, чтобы включить точку
```

Используется в трассировке ([26.03](26_03_Tracing_Shapes.md)) и в `Network Visibility` ([26.14](26_14_Network_Visibility.md)).

## Время и интерполяция

| Тип | Что делает |
|---|---|
| `TimeSince t = 0` | секунды, прошедшие с момента сброса (`if ( t > 5 ) ...`) |
| `TimeUntil t = 3` | секунды, оставшиеся до срабатывания |
| `RealTimeSince` / `RealTimeUntil` | то же, но независимо от `Time.Scale` |

Это **структуры с неявным приведением к `float`**, очень удобно:

```csharp
TimeSince _lastShoot;
if ( _lastShoot > 0.1f )
{
    Shoot();
    _lastShoot = 0;
}
```

## Полезные функции

```csharp
MathX.Lerp( a, b, t );                  // линейная интерполяция
MathX.Clamp( value, min, max );
MathX.Approach( current, target, step ); // плавно подползти к target
MathX.Remap( v, fromMin, fromMax, toMin, toMax );
```

## Что важно запомнить

- В s&box ось **Z — вертикальная**, **X — вперёд**, **Y — влево**.
- `Rotation` — кватернион (для математики), `Angles` — эйлер (для ввода/инспектора).
- Поворачивай вектор просто умножением: `rotation * vector`.
- `Transform` объединяет позицию+поворот+масштаб; `PointToWorld` / `PointToLocal` — для перехода между системами.
- `BBox` — выровненный по осям объём, нужен в трассировке и сетевой видимости.
- `TimeSince` / `TimeUntil` — самый удобный способ делать таймеры.

## Что дальше?

В [26.08](26_08_Curve_Easing.md) — кривые и easing-функции для плавной анимации.
