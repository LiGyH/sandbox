# 15.01 — Выброшенное оружие (DroppedWeapon) 🔫

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём **DroppedWeapon** — компонент для оружия, лежащего на земле. Игрок может подобрать его нажатием E.

## Как это работает?

`DroppedWeapon` реализует `IPressable` — при нажатии E появляется тултип с названием оружия и происходит подбор. Также реализует `PlayerController.IEvents` для интеграции с контроллером игрока.

### Защита от подбора контрапций

Если у оружия подключён `ClientInput` (привязано к кнопке через ControlSystem), подбор блокируется — нельзя забрать «стреляющее турелью» оружие.

## Создай файл

Путь: `Code/Items/DroppedWeapon.cs`

```csharp
public sealed class DroppedWeapon : Component, Component.IPressable, PlayerController.IEvents
{
	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return null;

		var name = weapon.DisplayName.ToUpper();

		if ( HasInput() ) return new IPressable.Tooltip( "Can't pick this up", "block", name );
		return new IPressable.Tooltip( "Pick up", "inventory_2", name );
	}

	private bool HasInput()
	{
		var weapon = GetComponent<BaseWeapon>();
		if ( !weapon.IsValid() ) return false;
		return weapon.ShootInput.IsEnabled || weapon.SecondaryInput.IsEnabled;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		//
		// Can't pick up weapons that are fireable by a contraption
		//
		if ( HasInput() ) return false;

		return true;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		DoPickup( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void DoPickup( GameObject presserObject )
	{
		if ( !presserObject.IsValid() ) return;

		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		TakeIntoInventory( inventory );
	}

	/// <summary>
	/// Disables world-physics components and moves the weapon into the player's inventory.
	/// </summary>
	private void TakeIntoInventory( PlayerInventory inventory )
	{
		var weapon = GetComponent<BaseCarryable>();
		if ( !weapon.IsValid() ) return;

		Enabled = false;

		inventory.Take( weapon, true );
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `IPressable.GetTooltip` | Тултип при наведении: «Pick up SHOTGUN» или «Can't pick this up» |
| `HasInput()` | Проверяет, привязано ли оружие к контрапции |
| `DoPickup` (`[Rpc.Host]`) | Подбор выполняется на хосте (серверная авторизация) |
| `inventory.Take(weapon, true)` | Перемещает оружие из мира в инвентарь |
| `Enabled = false` | Отключаем DroppedWeapon после подбора |

## Результат

- Оружие можно бросить на землю и подобрать обратно
- Тултип показывает название оружия
- Контрапционное оружие нельзя подобрать

---

Следующий шаг: [13.02 — Базовый подбираемый предмет (BasePickup)](13_02_BasePickup.md)
