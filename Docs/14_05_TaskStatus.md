# 14.05 — NPC: Статус задачи (TaskStatus) 📊

## Что мы делаем?

Создаём **TaskStatus** — перечисление, определяющее возможные статусы задачи NPC.

## Создай файл

Путь: `Code/Npcs/TaskStatus.cs`

```csharp
namespace Sandbox.Npcs;

/// <summary>
/// Status returned by task execution
/// </summary>
public enum TaskStatus
{
	/// <summary>
	/// Task is still running, continue executing
	/// </summary>
	Running,
	
	/// <summary>
	/// Task completed successfully
	/// </summary>
	Success,
	
	/// <summary>
	/// Task failed or was cancelled
	/// </summary>
	Failed,
	
	/// <summary>
	/// Task was interrupted by conditions
	/// </summary>
	Interrupted
}
```

| Статус | Когда |
|--------|-------|
| `Running` | Задача ещё выполняется, вызывай `OnUpdate` снова |
| `Success` | Задача завершена успешно — переходим к следующей |
| `Failed` | Задача провалилась — расписание прерывается |
| `Interrupted` | Расписание отменено (ShouldCancel вернул true) |

---

Следующий шаг: [14.06 — NPC: Базовая задача (TaskBase)](14_06_TaskBase.md)
