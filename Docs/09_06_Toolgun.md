# 🔫 Toolgun.cs — Основной класс оружия-тулгана

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём файл `Toolgun.cs` — основной класс оружия Toolgun, которое является **контейнером** для всех инструментов (`ToolMode`). Это `partial class`, основная часть логики.

## Зачем это нужно?

Toolgun — это оружие, которое **не стреляет пулями**, а переключается между инструментами. Каждый инструмент — это компонент `ToolMode`, прикреплённый к тому же `GameObject`. Файл `Toolgun.cs` отвечает за:

1. **Создание** всех инструментов (`CreateToolComponents`)
2. **Переключение** между инструментами (`SetToolMode`)
3. **Управление** активным инструментом (`OnControl`)
4. **Отрисовку** HUD через активный инструмент (`DrawHud`)
5. **Перехват** мыши для активного инструмента (`OnCameraMove`)

## Как это работает внутри движка?

### Наследование

```
BaseCarryable (оружие, которое можно носить)
  └── BaseWeapon
        └── ScreenWeapon (оружие с экраном на viewmodel)
              └── Toolgun (наш класс)
```

`ScreenWeapon` — подкласс из фазы 7 (CameraWeapon и ScreenWeapon), который добавляет возможность рисовать на маленьком экране модели оружия.

### Архитектура инструментов

```
GameObject "Toolgun"
  ├── Toolgun (Component)          — оружие
  ├── Weld (Component, enabled)    — активный инструмент
  ├── Rope (Component, disabled)   — неактивный
  ├── Remover (Component, disabled)— неактивный
  └── ... (все ToolMode)
```

Только **один** `ToolMode` включён в каждый момент. `GetCurrentMode()` возвращает первый **включённый** компонент `ToolMode`.

### Создание инструментов

```csharp
foreach ( var mode in Game.TypeLibrary.GetTypes<ToolMode>() )
{
    if ( mode.IsAbstract ) continue;
    Components.Create( mode, enabled ); // первый — enabled, остальные — disabled
    enabled = false;
}
```

`TypeLibrary.GetTypes<ToolMode>()` — находит **все** классы-наследники `ToolMode` в проекте. Это значит, что новые инструменты автоматически появляются в тулгане!

### Переключение инструментов

`SetToolMode(name)` — RPC-метод на хосте:
1. Находит тип по имени через `TypeLibrary`
2. Находит соответствующий компонент
3. Отключает текущий, включает новый
4. Обновляет сеть (`Network.Refresh`)
5. Отправляет RPC клиенту для анимации переключения

## Ссылки на движок

- [`Game.TypeLibrary.GetTypes<T>()`](https://github.com/LiGyH/sbox-public) — получение всех наследников типа
- [`Rpc.Host`](https://github.com/LiGyH/sbox-public) — RPC на хосте
- [`Network.Refresh()`](https://github.com/LiGyH/sbox-public) — синхронизация состояния объекта по сети

## Создай файл

📄 `Code/Weapons/ToolGun/Toolgun.cs`

```csharp
﻿using Sandbox.Rendering;

public partial class Toolgun : ScreenWeapon
{
	public override void OnCameraMove( Player player, ref Angles angles )
	{
		base.OnCameraMove( player, ref angles );

		var currentMode = GetCurrentMode();
		if ( currentMode is { AbsorbMouseInput: true } )
		{
			angles = default;
		}

		currentMode?.OnCameraMove( player, ref angles );
	}

	public override void OnAdded( Player player )
	{
		base.OnAdded( player );

		if ( Networking.IsHost )
			CreateToolComponents();
	}

	public void CreateToolComponents()
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "CreateToolComponents should be called on the host" );
			return;
		}

		bool enabled = true;

		foreach ( var mode in Game.TypeLibrary.GetTypes<ToolMode>() )
		{
			if ( mode.IsAbstract ) continue;

			Components.Create( mode, enabled );
			enabled = false;
		}
	}

	public override void OnControl( Player player )
	{
		var currentMode = GetCurrentMode();
		if ( currentMode == null )
			return;

		currentMode.OnControl();

		UpdateViewmodelScreen();

		base.OnControl( player );

		ApplyCoilSpin();
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		var currentMode = GetCurrentMode();
		currentMode?.DrawHud( painter, crosshair );
	}

	public ToolMode GetCurrentMode() => GetComponent<ToolMode>();

	public T GetMode<T>() where T : ToolMode
	{
		return GetComponent<T>( true );
	}

	[Rpc.Host]
	public void SetToolMode( string name )
	{
		var targetMode = Game.TypeLibrary.GetType<ToolMode>( name );
		if ( targetMode == null )
		{
			Log.Warning( $"Unknown Mode {name}" );
			return;
		}

		var newMode = GetComponents<ToolMode>( true ).Where( x => x.GetType() == targetMode.TargetType ).FirstOrDefault();
		if ( newMode == null )
		{
			Log.Warning( $"Toolgun missing mode component for {name}" );
			return;
		}

		var currentMode = GetCurrentMode();

		if ( newMode == currentMode )
			return;

		currentMode?.Enabled = false;
		newMode.Enabled = true;

		GameObject.Enabled = true;
		Network.Refresh( GameObject );

		using ( Rpc.FilterInclude( Rpc.Caller ) )
		{
			BroadcastSwitchToolMode();
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void BroadcastSwitchToolMode()
	{
		SwitchToolMode();
	}
}
```

## Разбор ключевых методов

| Метод | Описание |
|-------|----------|
| `OnCameraMove` | Если активный инструмент поглощает мышь (`AbsorbMouseInput`), обнуляет углы камеры |
| `OnAdded` | При экипировке на хосте создаёт все компоненты инструментов |
| `CreateToolComponents` | Перебирает все типы-наследники `ToolMode`, создаёт компоненты |
| `OnControl` | Вызывает `OnControl()` активного инструмента, обновляет экран |
| `DrawHud` | Делегирует отрисовку HUD активному инструменту |
| `GetCurrentMode` | Возвращает первый включённый `ToolMode` |
| `GetMode<T>` | Возвращает конкретный инструмент по типу (включая выключенные) |
| `SetToolMode` | RPC: переключает на указанный инструмент |

## Что проверить?

Toolgun наследуется от `ScreenWeapon`, который создан в фазе 7. Убедитесь, что `ScreenWeapon` уже существует. После создания этого файла можно будет видеть Toolgun в игре, но ему нужны ещё `Toolgun.Screen.cs` и `Toolgun.Effects.cs`.

---

➡️ Следующий шаг: [09_07 — Toolgun.Screen.cs и Toolgun.Effects.cs](09_07_Toolgun_Screen_Effects.md)
