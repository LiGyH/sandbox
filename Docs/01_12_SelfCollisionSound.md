# 01.12 — Звук столкновений (SelfCollisionSound)

## Что мы делаем?

Этот компонент управляет **звуками столкновений** физических объектов. По умолчанию движок проигрывает звуки от **обеих** поверхностей при столкновении (например, металл по бетону = звук металла + звук бетона). Этот компонент отключает стандартное поведение и проигрывает звук только от **своей** поверхности.

Аналогия: представьте, что вы уронили металлическую кастрюлю на кафельный пол. Без этого компонента вы услышите звук металла И звук кафеля. С компонентом — только звук металла (того объекта, к которому прикреплён компонент).

## Как это работает внутри движка?

### `ICollisionListener`

Интерфейс `Component.ICollisionListener` — это подписка на события физических столкновений. Движок автоматически вызывает `OnCollisionStart` при каждом столкновении объекта с чем-либо.

### `Surface`

`Surface` — тип поверхности в s&box (дерево, металл, бетон и т.д.). У каждой поверхности есть свои звуки столкновений, шагов, следов от пуль.

### `PhysicsBody.EnableCollisionSounds`

Флаг, включающий/отключающий стандартные звуки столкновений движка. Мы отключаем его и берём управление на себя.

## Создай файл

Путь: `Code/Utility/SelfCollisionSound.cs`

```csharp
/// <summary>
/// Only play the collision sound from this object's surface - not both the impacting surface and this surface.
/// </summary>
public sealed class SelfCollisionSound : Component, Component.ICollisionListener
{
	protected override void OnStart()
	{
		// Turn off default collision sounds
		foreach ( var body in GetComponents<Rigidbody>().Select( x => x.PhysicsBody ) )
		{
			body.EnableCollisionSounds = false;
		}
	}
	void ICollisionListener.OnCollisionStart( Sandbox.Collision collision )
	{
		Play( collision.Self.Shape, collision.Self.Surface, collision.Contact.Point, MathF.Abs( collision.Contact.NormalSpeed ) );
	}

	public void Play( PhysicsShape shape, Surface surface, in Vector3 position, float speed )
	{
		if ( !shape.IsValid() ) return;
		if ( !shape.Body.IsValid() ) return;
		if ( speed < 50.0f ) return;
		if ( surface == null ) return;

		surface.PlayCollisionSound( position, speed );
	}
}
```

## Разбор кода

### Объявление класса

```csharp
public sealed class SelfCollisionSound : Component, Component.ICollisionListener
```
- `Component` — базовый класс для компонентов s&box.
- `ICollisionListener` — интерфейс для получения событий столкновений.

### Инициализация — `OnStart`

```csharp
protected override void OnStart()
{
    foreach ( var body in GetComponents<Rigidbody>().Select( x => x.PhysicsBody ) )
    {
        body.EnableCollisionSounds = false;
    }
}
```
1. `GetComponents<Rigidbody>()` — находим все компоненты `Rigidbody` на этом объекте.
2. `.Select(x => x.PhysicsBody)` — LINQ: из каждого Rigidbody извлекаем физическое тело.
3. `body.EnableCollisionSounds = false` — отключаем стандартные звуки движка.

### Обработка столкновения

```csharp
void ICollisionListener.OnCollisionStart( Sandbox.Collision collision )
{
    Play(
        collision.Self.Shape,         // Форма нашего объекта
        collision.Self.Surface,       // Поверхность нашего объекта
        collision.Contact.Point,      // Точка столкновения
        MathF.Abs( collision.Contact.NormalSpeed ) // Скорость удара
    );
}
```
- `collision.Self` — **наш** объект (не тот, с которым столкнулись).
- `collision.Contact.NormalSpeed` — скорость столкновения по нормали. `MathF.Abs()` берёт модуль (скорость всегда положительная).

### Воспроизведение звука — `Play`

```csharp
public void Play( PhysicsShape shape, Surface surface, in Vector3 position, float speed )
{
    if ( !shape.IsValid() ) return;      // Форма уничтожена?
    if ( !shape.Body.IsValid() ) return; // Тело уничтожено?
    if ( speed < 50.0f ) return;         // Слишком слабый удар — без звука
    if ( surface == null ) return;       // Нет поверхности?

    surface.PlayCollisionSound( position, speed );
}
```
Серия проверок перед воспроизведением:
1. Форма и тело должны быть валидны (не уничтожены).
2. Скорость удара должна быть ≥ 50 (фильтрация тихих касаний).
3. Поверхность должна существовать.

Если всё ок — `surface.PlayCollisionSound()` воспроизводит звук в указанной позиции с громкостью, зависящей от скорости.

### Ключевое слово `in`

```csharp
in Vector3 position
```
`in` — передача по ссылке без копирования и без возможности изменить. Оптимизация для структур (Vector3 — это struct).

## Результат

После создания этого файла:
- Физические объекты с этим компонентом проигрывают звук только от **своей** поверхности.
- Слабые касания (скорость < 50) игнорируются.
- Стандартные двойные звуки столкновений отключены.
- Звуки звучат естественнее и без дублирования.

---

Следующий шаг: [01.13 — Тряска окружения (EnvironmentShake)](01_13_EnvironmentShake.md)
