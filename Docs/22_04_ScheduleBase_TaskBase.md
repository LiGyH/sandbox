# 14.07 — NPC: Базовое расписание (ScheduleBase) 📅

## Что мы делаем?

Создаём **ScheduleBase** — абстрактный класс расписания. Расписание — это последовательность задач (TaskBase), выполняемых одна за другой.

## Поток работы

```
OnStart()           ← наследник добавляет задачи через AddTask()
    ↓
InternalUpdate()    ← тикает текущую задачу
    ↓ (Success)
Следующая задача    ← _currentTaskIndex++
    ↓ (все задачи завершены)
InternalEnd()       ← очистка
```

## Создай файл

Путь: `Code/Npcs/ScheduleBase.cs`

```csharp
﻿namespace Sandbox.Npcs;

/// <summary>
/// A schedule -- can be understood as a way to execute a sequence of tasks
/// </summary>
public abstract class ScheduleBase
{
	public Npc Npc { get; private set; }
	protected GameObject GameObject => Npc.GameObject;

	private List<TaskBase> _tasks = new();
	private int _currentTaskIndex = 0;

	/// <summary>
	/// Initialize the schedule with the Behavior context
	/// </summary>
	internal void InternalInit( Npc npc )
	{
		Npc = npc;
		_tasks.Clear();
		_currentTaskIndex = 0;

		// Build task sequence
		OnStart();

		// Start first task
		StartCurrentTask();
	}

	protected virtual void OnUpdate()
	{
		//
	}

	protected virtual bool ShouldCancel()
	{
		return false;
	}

	/// <summary>
	/// Called when task is ended because of ShouldCancel returning true
	/// </summary>
	protected virtual void OnCancelled()
	{

	}

	/// <summary>
	/// Called every frame while schedule is running
	/// </summary>
	internal TaskStatus InternalUpdate()
	{
		if ( _tasks.Count == 0 ) return TaskStatus.Failed;
		if ( _currentTaskIndex >= _tasks.Count ) return TaskStatus.Success;

		// Give schedule a chance to cancel itself, based on interuptions
		if ( ShouldCancel() )
		{
			OnCancelled();
			return TaskStatus.Interrupted;
		}

		var currentTask = _tasks[_currentTaskIndex];
		var status = currentTask.InternalUpdate();

		if ( status is not TaskStatus.Running )
		{
			currentTask.InternalEnd();

			if ( status is TaskStatus.Success )
			{
				_currentTaskIndex++;
				StartCurrentTask();
				return TaskStatus.Running;
			}

			return status;
		}

		return TaskStatus.Running;
	}

	/// <summary>
	/// Called once when schedule ends
	/// </summary>
	internal void InternalEnd()
	{
		// End current task if running
		if ( _currentTaskIndex < _tasks.Count )
		{
			_tasks[_currentTaskIndex].InternalEnd();
		}

		_currentTaskIndex = 0;

		OnEnd();
	}

	/// <summary>
	/// Called when this schedule starts -- this is where you can add tasks to run
	/// </summary>
	protected virtual void OnStart() { }

	/// <summary>
	/// Called when this schedule ends -- this is where you can clean stuff up
	/// </summary>
	protected virtual void OnEnd() { }

	/// <summary>
	/// Add a task to the sequence
	/// </summary>
	protected void AddTask( TaskBase task )
	{
		_tasks.Add( task );
	}

	/// <summary>
	/// Start the current task in sequence
	/// </summary>
	private void StartCurrentTask()
	{
		if ( _currentTaskIndex < _tasks.Count )
		{
			var task = _tasks[_currentTaskIndex];
			task.Initialize( this );
		}
	}

	/// <summary>
	/// Information about this schedule for debugging purposes
	/// </summary>
	public string GetDebugString()
	{
		if ( _currentTaskIndex >= _tasks.Count )
			return $"{GetType().Name}/(none)";

		var task = _tasks[_currentTaskIndex];

		return $"{GetType().Name}/{task.GetType().Name}";
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `AddTask()` | Наследники добавляют задачи в `OnStart()` |
| `ShouldCancel()` | Вызывается каждый кадр — можно прервать по условию |
| `OnCancelled()` | Вызывается при прерывании (очистка) |
| `_currentTaskIndex` | Указатель на текущую задачу в последовательности |
| `GetDebugString()` | Для отладочного оверлея: «CombatEngage/FireWeapon» |

## Результат

- Расписания выполняют задачи последовательно
- Можно прервать по условию (враг убежал, NPC ранен)
- Отладочная строка показывает текущее состояние

---

Следующий шаг: [14.08 — NPC: Базовый слой (BaseNpcLayer)](14_08_BaseNpcLayer.md)


---

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
