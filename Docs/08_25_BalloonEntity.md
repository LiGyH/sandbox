# 08.25 — Сущность воздушного шара (BalloonEntity) 🎈💥

## Что мы делаем?

Создаём компонент `BalloonEntity` — логику поведения воздушного шара в мире. Этот компонент обрабатывает получение урона и «лопание» шара с эффектами и звуком.

## Зачем это нужно?

`BalloonEntity` добавляет интерактивность шарам:
- Шары можно лопнуть выстрелом или любым уроном
- При лопании воспроизводится визуальный эффект (частицы) с цветом шара
- Проигрывается звук лопания
- Ведётся статистика лопнувших шаров для каждого игрока

## Как это работает внутри движка?

- **`IDamageable`** — интерфейс, позволяющий компоненту получать урон от оружия и других источников.
- **`OnDamage()`** — вызывается при получении урона. Только на хосте (`IsProxy` проверка). Записывает статистику атакующему и вызывает `Pop()`.
- **`Pop()`** — RPC-метод на хосте:
  1. Клонирует префаб эффекта (`PopEffect`) с позицией шара
  2. Красит частицы эффекта в цвет шара через `ITintable`
  3. Проигрывает звук лопания (`PopSound`), с фолбэком на стандартный звук
  4. Уничтожает `GameObject` шара
- **`[RequireComponent] Prop`** — шар обязан иметь компонент `Prop`, из которого берётся цвет (`Tint`).

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Balloon/BalloonEntity.cs`

```csharp
public class BalloonEntity : Component, Component.IDamageable
{
	[Property] public PrefabFile PopEffect { get; set; }
	[Property] public SoundEvent PopSound { get; set; }
	[RequireComponent] public Prop Prop { get; set; }

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy ) return;

		damage.Attacker?.GetComponent<Player>()?.PlayerData?.AddStat( "balloon.popped" );

		Pop();
	}

	[Rpc.Host]
	private void Pop()
	{
		if ( PopEffect.IsValid() )
		{
			var effect = GameObject.Clone( PopEffect, new CloneConfig { Transform = WorldTransform, StartEnabled = false } );

			foreach ( var tintable in effect.GetComponentsInChildren<ITintable>( true ) )
			{
				tintable.Color = Prop.Tint;
			}

			effect.NetworkSpawn( true, null );
		}

		if ( PopSound is null )
		{
			PopSound = ResourceLibrary.Get<SoundEvent>( "entities/balloon/sounds/balloon_pop.sound" );
		}

		if ( PopSound is not null )
		{
			Sound.Play( PopSound, WorldPosition );
		}

		GameObject.Destroy();
	}
}
```

## Проверка

- Класс `BalloonEntity` наследуется от `Component` и реализует `Component.IDamageable`.
- `OnDamage()` проверяет `IsProxy` — урон обрабатывается только на хосте.
- Статистика `"balloon.popped"` записывается атакующему игроку.
- `Pop()` — `[Rpc.Host]` метод, создающий эффект, звук и уничтожающий шар.
- Эффект лопания окрашивается в цвет шара через `ITintable` интерфейс.
- Фолбэк звука: если `PopSound` не задан, загружается стандартный звук из ресурсов.
- `[RequireComponent]` гарантирует наличие `Prop` компонента на объекте.
