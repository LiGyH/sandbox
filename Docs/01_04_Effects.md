# 01.04 — Система эффектов (Effects)

## Что мы делаем?

Этот файл создаёт **систему визуальных эффектов** — конкретно, эффект брызг крови. Когда пуля попадает в игрока, система бросает луч (ray) от точки попадания в обратном направлении, находит ближайшую поверхность и «рисует» на ней пятно крови (декаль).

Представьте сцену из фильма: персонажа ранят, и на стене за ним появляется кровавое пятно. Этот код делает именно это.

## Как это работает внутри движка?

### `GameObjectSystem<T>`

`GameObjectSystem` — это **синглтон-система** в s&box. Она создаётся один раз на сцену и доступна через `Effects.Current`. Идеально для глобальных вещей, которые не привязаны к конкретному объекту.

### Primary Constructor

```csharp
public class Effects( Scene scene ) : GameObjectSystem<Effects>( scene )
```
Это **primary constructor** из C# 12: параметр `scene` объявлен прямо в заголовке класса.

### Cloud Materials

`Cloud.Material("jase.bloodsplatter08")` загружает материал из облачного хранилища s&box по его идентификатору. Это как скачать текстуру из магазина ассетов.

### Трассировка лучей (Raycasting)

`Game.ActiveScene.Trace.Ray(...)` — бросает луч из точки в направлении и проверяет, во что он попал. Это основной инструмент для обнаружения поверхностей.

## Создай файл

Путь: `Code/Utility/Effects.cs`

```csharp
namespace Sandbox;

public class Effects( Scene scene ) : GameObjectSystem<Effects>( scene )
{
	public List<Material> BloodDecalMaterials { get; set; } =
	[
		Cloud.Material( "jase.bloodsplatter08" ),
		Cloud.Material( "jase.bloodsplatter07" ),
		Cloud.Material( "jase.bloodsplatter06" ),
		Cloud.Material( "jase.bloodsplatter05" ),
		Cloud.Material( "jase.bloodsplatter04" )
	];

	public void SpawnBlood( Vector3 hitPosition, Vector3 direction, float damage = 50.0f )
	{
		const float BloodEjectDistance = 256.0f;
		var tr = Game.ActiveScene.Trace.Ray( new Ray( hitPosition, -direction ), BloodEjectDistance )
			.WithoutTags( "player" )
			.Run();

		if ( !tr.Hit ) return;

		var material = Random.Shared.FromList( BloodDecalMaterials );
		if ( !material.IsValid() ) return;

		var gameObject = Game.ActiveScene.CreateObject();
		gameObject.Name = "Blood splatter";
		gameObject.WorldPosition = tr.HitPosition + tr.Normal;
		gameObject.WorldRotation = Rotation.LookAt( -tr.Normal );
		gameObject.WorldRotation *= Rotation.FromAxis( Vector3.Forward, Game.Random.Float( 0, 360 ) );
	}
}
```

## Разбор кода

### Объявление класса

```csharp
public class Effects( Scene scene ) : GameObjectSystem<Effects>( scene )
```
- `Effects` — класс-система, один экземпляр на сцену.
- `GameObjectSystem<Effects>` — базовый класс синглтона. Позволяет обращаться как `Effects.Current`.
- `( Scene scene )` — primary constructor, сцена передаётся движком автоматически.

### Список материалов крови

```csharp
public List<Material> BloodDecalMaterials { get; set; } =
[
    Cloud.Material( "jase.bloodsplatter08" ),
    // ...
];
```
- Список из 5 текстур крови, загружаемых из облака.
- `[...]` — синтаксис коллекций C# 12 (collection expressions).
- При попадании случайно выбирается одна из них — для разнообразия.

### Метод `SpawnBlood`

```csharp
public void SpawnBlood( Vector3 hitPosition, Vector3 direction, float damage = 50.0f )
```
- `hitPosition` — точка попадания пули в игрока.
- `direction` — направление выстрела.
- `damage` — урон (по умолчанию 50, пока не используется в логике).

### Трассировка луча

```csharp
const float BloodEjectDistance = 256.0f;
var tr = Game.ActiveScene.Trace.Ray( new Ray( hitPosition, -direction ), BloodEjectDistance )
    .WithoutTags( "player" )
    .Run();
```
1. Бросаем луч из точки попадания в **обратном** направлении (`-direction`) — кровь летит «от пули».
2. Максимальная дальность — 256 юнитов.
3. `.WithoutTags("player")` — игнорируем самих игроков (нам нужна стена/пол).
4. `.Run()` — выполняем трассировку.

### Проверка попадания

```csharp
if ( !tr.Hit ) return;
```
Если луч ни во что не попал — выходим (нет стены поблизости).

### Выбор материала

```csharp
var material = Random.Shared.FromList( BloodDecalMaterials );
if ( !material.IsValid() ) return;
```
Случайный выбор текстуры крови. Если материал не загружен — выходим.

### Создание объекта декали

```csharp
var gameObject = Game.ActiveScene.CreateObject();
gameObject.Name = "Blood splatter";
gameObject.WorldPosition = tr.HitPosition + tr.Normal;
gameObject.WorldRotation = Rotation.LookAt( -tr.Normal );
gameObject.WorldRotation *= Rotation.FromAxis( Vector3.Forward, Game.Random.Float( 0, 360 ) );
```
1. Создаём новый пустой объект на сцене.
2. Позиция = точка попадания луча + немного «от стены» (чтобы не «врастать» в поверхность).
3. Поворот: «смотрит» от стены (`-tr.Normal`).
4. Случайный поворот вокруг оси (0–360°) — чтобы пятно не было одинаковым каждый раз.

## Результат

После создания этого файла:
- В проекте появляется система `Effects`, доступная через `Effects.Current`.
- Метод `SpawnBlood()` создаёт случайное пятно крови на ближайшей поверхности при попадании.
- Система готова к использованию в логике урона.

---

Следующий шаг: [01.05 — Масштабирование интерфейса (HUD)](01_05_HUD.md)
