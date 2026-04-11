# 🔧 Toolgun — Основная логика инструментальной пушки

## Что мы делаем?

Создаём файл `Toolgun.cs` — основной класс инструментальной пушки (Toolgun), которая позволяет игроку использовать различные инструменты (ToolMode) для создания, модификации и управления объектами.

## Зачем это нужно?

Toolgun — второй ключевой инструмент в песочнице. Он:
- Поддерживает множество режимов (сварка, верёвка, колёса, ховерболлы и т.д.)
- Переключает режимы динамически через `SetToolMode`
- Делегирует управление текущему активному `ToolMode`
- Создаёт компоненты всех доступных инструментов при добавлении оружия игроку
- Отрисовывает HUD и экран через текущий режим

## Как это работает внутри движка?

- `OnAdded` — при добавлении оружия игроку, хост создаёт компоненты для всех зарегистрированных `ToolMode` через рефлексию (`Game.TypeLibrary.GetTypes<ToolMode>()`)
- `OnControl` — делегирует управление текущему режиму через `GetCurrentMode().OnControl()`
- `GetCurrentMode` — возвращает первый активный `ToolMode` компонент
- `SetToolMode` — RPC-метод (Host): отключает текущий режим и включает запрошенный, затем обновляет сеть
- `DrawHud` — делегирует отрисовку HUD текущему режиму
- `OnCameraMove` — позволяет текущему режиму поглощать ввод мыши (`AbsorbMouseInput`)
- `BroadcastSwitchToolMode` — отправляет RPC вызвавшему клиенту для визуального переключения

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

## Проверка

- Файл `Code/Weapons/ToolGun/Toolgun.cs` создан как partial-класс `Toolgun : ScreenWeapon`
- `CreateToolComponents` использует рефлексию для создания всех ToolMode
- `SetToolMode` — RPC-метод для сетевого переключения режимов
- `GetCurrentMode` возвращает активный `ToolMode` компонент
