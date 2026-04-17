# 16.01 — Базовый переключатель (BaseToggle) 🔀

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

## Что мы делаем?

Создаём **BaseToggle** — абстрактный компонент с синхронизированным состоянием вкл/выкл. Это базовый класс для интерактивных объектов карты.

## Как это работает?

`BaseToggle` хранит булево свойство `State`, синхронизированное по сети через `[Sync]`. При изменении состояния вызывается делегат `OnStateChanged`, который могут использовать другие компоненты (например, для включения света, открытия ворот и т.д.).

## Создай файл

Путь: `Code/Map/BaseToggle.cs`

```csharp
﻿public abstract class BaseToggle : Component
{
	public delegate Task StateChangedDelegate( bool state );

	/// <summary>
	/// The toggle state has changed
	/// </summary>
	[Property] public StateChangedDelegate OnStateChanged { get; set; }

	bool _state;

	[Property, Sync]
	public bool State
	{
		get => _state;
		set
		{
			if ( _state == value ) return;

			_state = value;
			StateHasChanged( _state );

		}
	}

	/// <summary>
	/// The toggle state has changed
	/// </summary>
	protected virtual void StateHasChanged( bool newState )
	{
		OnStateChanged?.Invoke( _state );
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `[Sync]` | Синхронизирует `State` между хостом и клиентами |
| `StateChangedDelegate` | Делегат, возвращающий `Task` — поддерживает асинхронные реакции |
| `StateHasChanged` | Виртуальный метод — наследники могут добавить свою логику |
| `[Property]` на делегате | Позволяет привязать обработчик через Action Graph в редакторе |

## Результат

- Готова база для создания переключаемых объектов карты
- Состояние синхронизируется по сети автоматически

---

Следующий шаг: [12.03 — Дверь (Door)](16_02b_Door.md)
