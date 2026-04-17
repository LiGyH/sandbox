# 03.09 — Фонарик игрока (PlayerFlashlight) 🔦

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём **фонарик** — направленный свет (`SpotLight`), который прикреплён к голове игрока и включается/выключается клавишей F. Состояние синхронизируется по сети — другие игроки видят ваш фонарик.

## Как это работает?

### Компоненты

```csharp
[Property, RequireComponent] public SpotLight Light { get; set; }
```

`SpotLight` — компонент направленного света s&box. `RequireComponent` — если на объекте нет SpotLight, он будет создан автоматически.

### Синхронизация состояния

```csharp
[Sync, Change( nameof( UpdateLight ) )] public bool IsOn { get; set; } = false;
```

- `[Sync]` — значение синхронизируется по сети
- `[Change( nameof( UpdateLight ) )]` — при изменении значения вызывается метод `UpdateLight()`
- По умолчанию выключен (`false`)

### Toggle (включение/выключение)

```csharp
private void Toggle()
{
    BroadcastToggle( !IsOn );
    
    var sound = IsOn ? ToggleOnSound : ToggleOffSound;
    if ( sound.IsValid() )
        Sound.Play( sound, WorldPosition );
}

[Rpc.Broadcast]
private void BroadcastToggle( bool value )
{
    IsOn = value;
}
```

1. `Toggle()` вызывается при нажатии клавиши
2. `BroadcastToggle` — RPC на всех клиентах, обновляет `IsOn`
3. Изменение `IsOn` срабатывает `[Change]` → `UpdateLight()` включает/выключает свет
4. Звук проигрывается локально

### Следование за головой

```csharp
protected override void OnUpdate()
{
    WorldTransform = _player.EyeTransform.ToWorld( _localOffset );
}
```

Каждый кадр фонарик перемещается к позиции глаз игрока с учётом локального смещения (`_localOffset`). Это даёт эффект «фонарик на каске» — светит туда, куда смотришь.

## Создай файл

Путь: `Code/Player/PlayerFlashlight.cs`

```csharp
public sealed class PlayerFlashlight : Component
{
	[Property, RequireComponent] public SpotLight Light { get; set; }

	[Property, Group( "Sound" )] public SoundEvent ToggleOnSound { get; set; }
	[Property, Group( "Sound" )] public SoundEvent ToggleOffSound { get; set; }
	[Sync, Change( nameof( UpdateLight ) )] public bool IsOn { get; set; } = false;

	private Player _player;
	private Transform _localOffset;

	protected override void OnStart()
	{
		_player = GetComponentInParent<Player>();
		_localOffset = LocalTransform;
		UpdateLight();
	}

	protected override void OnUpdate()
	{
		if ( !_player.IsValid() ) return;

		if ( !IsProxy && Input.Pressed( "Flashlight" ) )
		{
			Toggle();
		}

		WorldTransform = _player.EyeTransform.ToWorld( _localOffset );
	}

	private void Toggle()
	{
		BroadcastToggle( !IsOn );

		var sound = IsOn ? ToggleOnSound : ToggleOffSound;
		if ( sound.IsValid() )
		{
			Sound.Play( sound, WorldPosition );
		}
	}

	[Rpc.Broadcast]
	private void BroadcastToggle( bool value )
	{
		IsOn = value;
	}

	private void UpdateLight()
	{
		if ( !Light.IsValid() ) return;
		
		Light.Enabled = IsOn;
	}
}
```

## Ключевые концепции

### Transform.ToWorld

```csharp
WorldTransform = _player.EyeTransform.ToWorld( _localOffset );
```

`ToWorld` конвертирует локальный transform в мировой, относительно другого transform. Т.е.: «возьми позицию глаз, примени к ней моё локальное смещение».

### Зачем _localOffset?

При старте запоминаем: `_localOffset = LocalTransform`. Это смещение фонарика относительно родителя (обычно несколько сантиметров вперёд и вправо от глаз). Без него фонарик был бы точно в центре головы.

### [Sync, Change] — паттерн реактивности

Когда другой клиент меняет `IsOn` через сеть, `[Change]` автоматически вызывает `UpdateLight()`. Нам не нужно проверять `IsOn` каждый кадр в `OnUpdate()` — это эффективнее.

## Проверка

1. Нажми F → фонарик включается, звук
2. Нажми F снова → выключается
3. Другой игрок видит твой фонарик

## Следующий файл

Переходи к **03.11 — Гибы игрока (PlayerGib)**.
