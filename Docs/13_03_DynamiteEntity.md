# Этап 13_03 — DynamiteEntity (Динамит — взрывная сущность)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.16 — Prefabs](00_16_Prefabs.md)

## Что это такое?

`DynamiteEntity` — это компонент, превращающий объект в динамит. Представьте красную шашку динамита из мультфильмов: вы нажимаете кнопку — и **БУМ!** — всё вокруг разлетается. Именно так работает этот компонент:

- Игрок нажимает назначенную клавишу → взрыв
- Динамит получает урон (пуля, другой взрыв) → тоже взрыв
- Взрыв наносит урон и отбрасывает всё в заданном радиусе

## Как это работает?

```
DynamiteEntity
├── Damage = 128       → урон взрыва
├── Radius = 1024      → радиус взрыва (в юнитах)
├── Force  = 1         → сила отбрасывания
├── Activate           → привязка клавиши для подрыва
│
├── Explode()          → [Rpc.Host] основная логика взрыва
├── OnDamage()         → получил урон → взрываемся
└── OnControl()        → игрок нажал кнопку → взрываемся
```

## Исходный код

📁 `Code/Game/Entity/DynamiteEntity.cs`

```csharp
[Alias( "dynamite" )]
public class DynamiteEntity : Component, IPlayerControllable, Component.IDamageable
{
	[Property, Range( 1, 500 ), Step( 1 ), ClientEditable]
	public float Damage { get; set; } = 128;

	[Property, Range( 16, 4096 ), Step( 16 ), ClientEditable]
	public float Radius { get; set; } = 1024f;

	[Property, Range( 1, 100 ), Step( 1 ), ClientEditable]
	public float Force { get; set; } = 1;

	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	bool _isDead = false;

	[Rpc.Host]
	public void Explode()
	{
		_isDead = true;

		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
		if ( explosionPrefab == null )
		{
			Log.Warning( "Can't find /prefabs/engine/explosion_med.prefab" );
			return;
		}

		var go = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = WorldTransform.WithScale( 1 ), StartEnabled = false } );
		if ( !go.IsValid() ) return;

		go.RunEvent<RadiusDamage>( x =>
		{
			x.Radius = Radius;
			x.PhysicsForceScale = Force;
			x.DamageAmount = Damage;
			x.Attacker = go;
		}, FindMode.EverythingInSelfAndDescendants );

		go.Enabled = true;
		go.NetworkSpawn( true, null );

		GameObject.Destroy();
	}

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( _isDead ) return;
		if ( IsProxy ) return;

		Explode();
	}

	void IPlayerControllable.OnControl()
	{
		if ( Activate.Pressed() )
		{
			Explode();
		}
	}

	void IPlayerControllable.OnEndControl()
	{
		// nothing to do
	}

	void IPlayerControllable.OnStartControl()
	{
		// nothing to do
	}
}
```

## Разбор кода

### Атрибут класса

```csharp
[Alias( "dynamite" )]
public class DynamiteEntity : Component, IPlayerControllable, Component.IDamageable
```

- **`[Alias("dynamite")]`** — псевдоним для сериализации. Если в сохранённых данных встретится тип `"dynamite"`, движок поймёт, что это `DynamiteEntity`.
- **`Component`** — базовый класс: динамит живёт на `GameObject` в сцене.
- **`IPlayerControllable`** — позволяет игроку управлять динамитом (нажимать кнопку подрыва).
- **`Component.IDamageable`** — позволяет динамиту получать урон (и взрываться от него).

### Настраиваемые параметры

```csharp
[Property, Range( 1, 500 ), Step( 1 ), ClientEditable]
public float Damage { get; set; } = 128;

[Property, Range( 16, 4096 ), Step( 16 ), ClientEditable]
public float Radius { get; set; } = 1024f;

[Property, Range( 1, 100 ), Step( 1 ), ClientEditable]
public float Force { get; set; } = 1;
```

Три числа, описывающих мощность взрыва:

| Свойство | По умолчанию | Описание |
|----------|-------------|----------|
| `Damage` | 128 | Количество урона в центре взрыва |
| `Radius` | 1024 | Радиус поражения (в юнитах движка) |
| `Force` | 1 | Множитель физической силы отбрасывания |

Атрибуты `Range` и `Step` создают ползунок в редакторе. `ClientEditable` позволяет менять значения через контекстное меню (ПКМ по объекту в игре).

### Ввод игрока

```csharp
[Property, Sync, ClientEditable]
public ClientInput Activate { get; set; }
```

`ClientInput` — это привязка клавиши. Игрок может назначить любую кнопку через контекстное меню. `[Sync]` синхронизирует настройку между клиентами.

### Флаг «мёртв»

```csharp
bool _isDead = false;
```

Простой флаг, предотвращающий повторный взрыв. Без него динамит мог бы получить урон от собственного взрыва и «умереть» дважды.

### Метод `Explode()` — главная логика

```csharp
[Rpc.Host]
public void Explode()
```

`[Rpc.Host]` означает, что этот метод выполняется **только на сервере** (хосте). Даже если клиент вызовет его — запрос уйдёт на сервер.

Пошагово:

1. **Устанавливаем `_isDead = true`** — больше не реагируем на урон.

2. **Загружаем префаб взрыва:**
   ```csharp
   var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
   ```
   Из библиотеки ресурсов берётся готовый эффект взрыва (частицы, звук, свет).

3. **Клонируем префаб в мир:**
   ```csharp
   var go = GameObject.Clone( explosionPrefab, new CloneConfig {
       Transform = WorldTransform.WithScale( 1 ),
       StartEnabled = false
   } );
   ```
   Создаём объект взрыва в позиции динамита. `StartEnabled = false` — пока не включаем, сначала настроим.

4. **Настраиваем урон по площади:**
   ```csharp
   go.RunEvent<RadiusDamage>( x =>
   {
       x.Radius = Radius;
       x.PhysicsForceScale = Force;
       x.DamageAmount = Damage;
       x.Attacker = go;
   }, FindMode.EverythingInSelfAndDescendants );
   ```
   `RunEvent` находит компонент `RadiusDamage` в префабе взрыва и настраивает его: радиус, силу, урон. `FindMode.EverythingInSelfAndDescendants` ищет во всех дочерних объектах.

5. **Включаем и спавним в сеть:**
   ```csharp
   go.Enabled = true;
   go.NetworkSpawn( true, null );
   ```
   Теперь взрыв виден всем игрокам.

6. **Уничтожаем динамит:**
   ```csharp
   GameObject.Destroy();
   ```
   Шашка динамита больше не нужна — она «израсходована».

### Получение урона

```csharp
void IDamageable.OnDamage( in DamageInfo damage )
{
    if ( _isDead ) return;
    if ( IsProxy ) return;

    Explode();
}
```

Когда что-то наносит урон динамиту:
- Если уже мёртв (`_isDead`) — игнорируем.
- Если это прокси (клиентская копия, а не серверный объект) — игнорируем (решения принимает только сервер).
- Иначе — взрываемся!

Это создаёт **цепную реакцию**: один взрыв может повредить соседний динамит, и тот тоже взорвётся.

### Управление игроком

```csharp
void IPlayerControllable.OnControl()
{
    if ( Activate.Pressed() )
    {
        Explode();
    }
}
```

Когда игрок управляет динамитом и нажимает назначенную клавишу — подрыв.

```csharp
void IPlayerControllable.OnEndControl() { }
void IPlayerControllable.OnStartControl() { }
```

Эти методы обязательны по интерфейсу `IPlayerControllable`, но динамиту не нужна логика при начале/конце управления.


---

## ➡️ Следующий шаг

Переходи к **[13.04 — Этап 13_04 — Lights (Управляемые источники света)](13_04_Lights.md)**.
