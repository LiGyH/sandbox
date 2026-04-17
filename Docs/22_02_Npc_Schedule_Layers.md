# 22.02 — NPC: Планировщик расписаний (Npc.Schedule) 📋

## Что мы делаем?

Добавляем **Npc.Schedule.cs** — partial-часть, управляющую выбором и выполнением расписаний (Schedules).

## Как работает система расписаний

```
GetSchedule()              ← наследник выбирает расписание
    ↓
ActiveSchedule.InternalInit(npc)  ← инициализация + OnStart()
    ↓
ActiveSchedule.InternalUpdate()   ← каждый кадр, пока Running
    ↓
ActiveSchedule.InternalEnd()      ← завершение (Success/Failed/Interrupted)
    ↓
GetSchedule()              ← цикл начинается заново
```

## Создай файл

Путь: `Code/Npcs/Npc.Schedule.cs`

```csharp
﻿namespace Sandbox.Npcs;

public partial class Npc : Component
{
	/// <summary>
	/// The current running schedule for this NPC.
	/// </summary>
	public ScheduleBase ActiveSchedule { get; private set; }

	readonly Dictionary<Type, ScheduleBase> _schedules = [];

	/// <summary>
	/// Get a schedule -- if it doesn't exist, one will be created
	/// </summary>
	protected T GetSchedule<T>() where T : ScheduleBase, new()
	{
		var type = typeof(T);
		if ( !_schedules.TryGetValue( type, out var schedule ) )
		{
			schedule = new T();
			_schedules[type] = schedule;
		}

		return (T)schedule;
	}

	public virtual ScheduleBase GetSchedule()
	{
		return null;
	}

	/// <summary>
	/// Updates a behavior, returns if there is an active schedule - this will stop lower priority behaviors from running
	/// </summary>
	void TickSchedule()
	{
		// If we have a schedule, keep running it 
		// until it's completely finished.
		if ( ActiveSchedule is not null )
		{
			RunActiveSchedule();
			return;
		}

		var newSchedule = GetSchedule();
		if ( newSchedule is null ) return;

		ActiveSchedule = newSchedule;
		ActiveSchedule?.InternalInit( this );
	}

	private void RunActiveSchedule()
	{
		var status = ActiveSchedule.InternalUpdate();

		if ( status != TaskStatus.Running )
		{
			EndCurrentSchedule();
		}
	}


	protected override void OnDisabled()
	{
		EndCurrentSchedule();
	}

	/// <summary>
	/// End the current schedule cleanly. Can be called by subclasses to interrupt
	/// the active schedule (e.g. when damaged).
	/// </summary>
	protected void EndCurrentSchedule()
	{
		ActiveSchedule?.InternalEnd();
		ActiveSchedule = null;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `_schedules` | Кеш расписаний по типу — не создаём каждый раз новое |
| `GetSchedule<T>()` | Получить/создать расписание нужного типа |
| `virtual GetSchedule()` | Наследники переопределяют — выбирают расписание по ситуации |
| `TickSchedule()` | Вызывается каждый кадр из `OnUpdate` |
| `EndCurrentSchedule()` | Можно вызвать из наследника (при получении урона) |

## Результат

- NPC автоматически выбирает и выполняет расписания
- Кеширование расписаний — экономия памяти
- Прерывание расписания при событиях

---

Следующий шаг: [14.05 — NPC: Статус задачи (TaskStatus)](22_04_ScheduleBase_TaskBase.md)


---

# 14.03 — NPC: Ссылки на слои (Npc.Layers) 🧩

## Что мы делаем?

Добавляем **Npc.Layers.cs** — partial-часть, объявляющую обязательные слои (`[RequireComponent]`) NPC.

## Создай файл

Путь: `Code/Npcs/Npc.Layers.cs`

```csharp
﻿using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs;

public partial class Npc : Component
{
	/// <summary>
	/// Senses layer - handles environmental awareness and target detection
	/// </summary>
	[RequireComponent]
	public SensesLayer Senses { get; set; }

	/// <summary>
	/// Navigation layer - handles pathfinding and movement
	/// </summary>
	[RequireComponent]
	public NavigationLayer Navigation { get; set; }

	/// <summary>
	/// Animation layer - handles look-at and animation parameters
	/// </summary>
	[RequireComponent]
	public AnimationLayer Animation { get; set; }

	/// <summary>
	/// Speech layer - handles talking, lipsync, etc..
	/// </summary>
	[RequireComponent]
	public SpeechLayer Speech { get; set; }
}
```

## Что такое [RequireComponent]?

Атрибут `[RequireComponent]` указывает движку: «этот компонент ОБЯЗАН быть на том же GameObject». Если его нет — редактор автоматически добавит при добавлении NPC.

| Слой | Назначение |
|------|-----------|
| `SensesLayer` | Обнаружение целей (видимость, слух) |
| `NavigationLayer` | Навигация по NavMesh |
| `AnimationLayer` | Анимации, IK, удержание предметов |
| `SpeechLayer` | Речь, субтитры, звуки |

---

Следующий шаг: [14.04 — NPC: Планировщик расписаний (Npc.Schedule)](22_02_Npc_Schedule_Layers.md)
