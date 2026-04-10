# 14.04 — NPC: Планировщик расписаний (Npc.Schedule) 📋

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

Следующий шаг: [14.05 — NPC: Статус задачи (TaskStatus)](14_05_TaskStatus.md)
