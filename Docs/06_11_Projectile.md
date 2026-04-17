# 06.11 — Базовый снаряд (Projectile) 🚀

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.12 — Component и его жизненный цикл](00_12_Component.md)
> - [00.21 — Сетевые объекты](00_21_Networked_Objects.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

## Что мы делаем?

Создаём базовый класс `Projectile` — универсальный снаряд, который летит в пространстве, реагирует на столкновения и уничтожается при ударе. Это общий фундамент для гранат-снарядов (дальше появится [RPG-ракета](07_10_Rpg.md)) и любых будущих снарядов.

## Зачем это нужно?

- **Единый интерфейс для снарядов**: любая «летящая штука» с физикой выигрывает от базовой логики «удар → событие → уничтожение».
- **Корректное отношение к стрелявшему**: в `Instigator` хранится `PlayerData` того, кто выстрелил, и при столкновении со стрелком снаряд **не** взрывается — удобно для RPG, гранат и т.п.
- **Хост-авторитет**: `ICollisionListener.OnCollisionStart` сразу выходит, если компонент — прокси. Логика удара выполняется только на владельце/хосте.
- **Физика из коробки**: `[RequireComponent] Rigidbody` гарантирует, что на GameObject со снарядом всегда есть физическое тело. `UpdateDirection()` выставляет ориентацию и скорость одним вызовом.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `[RequireComponent] Rigidbody` | Принудительное наличие `Rigidbody` на том же GameObject. |
| `[Sync( SyncFlags.FromHost )] Instigator` | `PlayerData` стрелявшего. Реплицируется с хоста. |
| `TimeSinceCreated` | Время с момента включения, сбрасывается в `OnEnabled`. Полезно для тайм-аутов у наследников. |
| `OnStart()` | Добавляет тег `"projectile"` (для траcсировок и игнорирования). |
| `OnCollisionStart(collision)` | На non-proxy: если другая сторона — `Player` и это `Instigator`, игнорируем (не взрываемся в руках стрелка). Иначе — `OnHit(collision)`. |
| `OnHit(collision)` | `virtual` — базовая реализация просто уничтожает GameObject. Наследники переопределяют для взрыва, деколя и т.п. |
| `NotifyHit(target, point)` | Хелпер-заготовка: предполагается, что будет отправлять владельцу-стрелку уведомление о попадании (для хитмаркеров). На данный момент `// TODO: implement me`. |
| `UpdateDirection(direction, speed)` | Выставляет `WorldRotation = Rotation.LookAt(direction, Vector3.Up)` и `Rigidbody.Velocity = Forward * speed`. Используется при запуске и корректировке курса. |

## Создай файл

**Путь:** `Code/Game/Weapon/Projectile.cs`

```csharp
/// <summary>
/// A projectile. It explodes when it hits something.
/// </summary>
public class Projectile : Component, Component.ICollisionListener
{
	[RequireComponent] public Rigidbody Rigidbody { get; set; }

	[Sync( SyncFlags.FromHost )] public PlayerData Instigator { get; set; }

	protected TimeSince TimeSinceCreated;

	protected override void OnStart()
	{
		Tags.Add( "projectile" );
	}

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy )
			return;

		var player = collision.Other.GameObject.GetComponentInParent<Player>();
		if ( Instigator.IsValid() && player.IsValid() && player.PlayerData == Instigator )
		{
			return;
		}

		OnHit( collision );
	}

	protected virtual void OnHit( Collision collision = default )
	{
		GameObject.Destroy();
	}

	/// <summary>
	/// Tell the instigator their projectile hit, so they can do things like show a hitmarker
	/// </summary>
	protected void NotifyHit( IDamageable target, Vector3 point )
	{
		if ( !Instigator.IsValid() ) return;

		var connection = Instigator.Connection;
		if ( connection is null ) return;

		// TODO: implement me
	}

	/// <summary>
	/// Updates the current direction of the projectile
	/// </summary>
	public void UpdateDirection( Vector3 direction, float speed )
	{
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
		Rigidbody.Velocity = WorldRotation.Forward * speed;
	}
}
```

## Проверка

- [ ] Файл компилируется
- [ ] Добавление `Projectile` на GameObject автоматически требует `Rigidbody`
- [ ] GameObject получает тег `"projectile"` после старта
- [ ] Столкновение снаряда со стрелявшим игнорируется
- [ ] В проксях (`IsProxy == true`) `OnCollisionStart` ничего не делает
- [ ] `UpdateDirection` выставляет направление и скорость физического тела

## Смотри также

- [07.10 — RPG (RpgWeapon + RpgProjectile)](07_10_Rpg.md) — конкретный наследник с взрывом.
- [13.03 — DynamiteEntity](13_03_DynamiteEntity.md) — альтернативная «взрывная сущность» без `Projectile`-базы.

---

## ➡️ Следующий шаг

Переходи к **[06.06 — BaseBulletWeapon](06_06_BaseBulletWeapon.md)**.
