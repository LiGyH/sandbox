# 01.03 — Расширения векторов (Extensions)

## Что мы делаем?

Этот файл добавляет методы-расширения для типа `Vector3` — трёхмерного вектора (направления в пространстве). Главная задача — добавить **разброс** (aim cone) к направлению выстрела.

Когда игрок стреляет, пуля не всегда летит идеально прямо. В реальности есть разброс — «конус прицеливания». Эти методы берут направление (например, «вперёд от оружия») и случайно отклоняют его в заданных пределах, имитируя неточность стрельбы.

## Как это работает внутри движка?

### Разброс (Aim Cone)

Представьте конус, вершина которого — дуло оружия. Пуля может полететь в любую точку внутри этого конуса. Чем больше угол конуса — тем сильнее разброс.

### Rotation и Angles

- `Rotation.LookAt(direction)` — создаёт вращение, «смотрящее» в указанном направлении.
- `Angles` — углы Эйлера (pitch, yaw, roll) для поворотов.
- Умножение `Rotation *= Angles(...)` добавляет дополнительное отклонение.
- `.Forward` — извлекает новое направление «вперёд» из результирующего вращения.

## Создай файл

Путь: `Code/Utility/Extensions.cs`

```csharp
public static class Extensions
{
	public static Vector3 WithAimCone( this Vector3 direction, float degrees )
	{
		var angle = Rotation.LookAt( direction );
		angle *= new Angles( Game.Random.Float( -degrees / 2.0f, degrees / 2.0f ), Game.Random.Float( -degrees / 2.0f, degrees / 2.0f ), 0 );
		return angle.Forward;
	}

	public static Vector3 WithAimCone( this Vector3 direction, float horizontalDegrees, float verticalDegrees )
	{
		var angle = Rotation.LookAt( direction );
		angle *= new Angles( Game.Random.Float( -verticalDegrees / 2.0f, verticalDegrees / 2.0f ), Game.Random.Float( -horizontalDegrees / 2.0f, horizontalDegrees / 2.0f ), 0 );
		return angle.Forward;
	}
}
```

## Разбор кода

### Первый перегруз — симметричный конус

```csharp
public static Vector3 WithAimCone( this Vector3 direction, float degrees )
```
- `this Vector3 direction` — метод-расширение. Вызывается как `myVector.WithAimCone(5)`.
- `float degrees` — угол разброса в градусах (одинаковый по горизонтали и вертикали).

```csharp
var angle = Rotation.LookAt( direction );
```
Создаём вращение, направленное вдоль `direction`.

```csharp
angle *= new Angles(
    Game.Random.Float( -degrees / 2.0f, degrees / 2.0f ),  // pitch (вверх-вниз)
    Game.Random.Float( -degrees / 2.0f, degrees / 2.0f ),  // yaw (влево-вправо)
    0                                                        // roll (не меняем)
);
```
Добавляем случайное отклонение:
- **Pitch** — случайный угол от `-degrees/2` до `+degrees/2` (вертикальное отклонение).
- **Yaw** — аналогично, но горизонтальное.
- **Roll** — 0, потому что вращение «вокруг оси» не влияет на направление пули.

```csharp
return angle.Forward;
```
Извлекаем новый вектор направления после отклонения.

### Второй перегруз — асимметричный конус

```csharp
public static Vector3 WithAimCone( this Vector3 direction, float horizontalDegrees, float verticalDegrees )
```
То же самое, но горизонтальный и вертикальный разброс задаются **отдельно**. Это полезно, например, если оружие более точное по вертикали, но «гуляет» по горизонтали.

## Результат

После создания этого файла:
- Любой вектор направления можно «размыть» вызовом `.WithAimCone(5)` (5° разброса).
- Поддерживается как симметричный конус (`degrees`), так и асимметричный (`horizontalDegrees`, `verticalDegrees`).
- Это основа системы разброса оружия в проекте.

---

Следующий шаг: [01.04 — Система эффектов (Effects)](01_04_Effects.md)
