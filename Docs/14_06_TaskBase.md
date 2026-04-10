# 14.06 — NPC: Базовая задача (TaskBase) 📝

## Что мы делаем?

Создаём **TaskBase** — абстрактный базовый класс для всех задач NPC. Задача — это атомарное действие (идти, ждать, стрелять, говорить).

## Жизненный цикл задачи

```
Initialize(schedule)  ← привязка к расписанию
    ↓
OnStart()            ← вызывается один раз
    ↓
OnUpdate() → Status  ← вызывается каждый кадр
    ↓ (когда не Running)
OnEnd()              ← вызывается при завершении
    ↓
Reset()              ← очистка состояния
```

## Создай файл

Путь: `Code/Npcs/TaskBase.cs`

```csharp
﻿namespace Sandbox.Npcs;

/// <summary>
/// A task
/// </summary>
public abstract class TaskBase
{
	protected ScheduleBase Schedule { get; private set; }
	protected Npc Npc => Schedule.Npc;

	/// <summary>
	/// What is the current status of this task?
	/// </summary>
	protected TaskStatus Status { get; private set; }

	internal void Initialize( ScheduleBase schedule )
	{
		Schedule = schedule;
		InternalStart();
	}

	private void InternalStart()
	{
		Status = TaskStatus.Running;
		OnStart();
	}

	internal TaskStatus InternalUpdate()
	{
		Status = OnUpdate();
		return Status;
	}

	internal void InternalEnd()
	{
		if ( Status == TaskStatus.Running )
		{
			Status = TaskStatus.Success;
		}

		OnEnd();
		Reset();
	}

	protected virtual void OnStart() { }
	protected abstract TaskStatus OnUpdate();
	protected virtual void OnEnd() { }
	protected virtual void Reset() { }
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `Schedule.Npc` | Через расписание задача получает доступ к NPC |
| `internal` методы | Вызываются только из ScheduleBase (инкапсуляция) |
| `OnUpdate()` (abstract) | Обязательно переопределяется — возвращает TaskStatus |
| `Reset()` | Очищает внутреннее состояние для повторного использования |

---

Следующий шаг: [14.07 — NPC: Базовое расписание (ScheduleBase)](14_07_ScheduleBase.md)
