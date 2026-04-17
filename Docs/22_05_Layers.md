# 22.05 — NPC: Базовый слой (BaseNpcLayer) 🧱

## Что мы делаем?

Создаём **BaseNpcLayer** — абстрактный базовый класс для «слоёв» NPC. Слой — это сервисный компонент, предоставляющий функциональность задачам (сенсоры, навигация, анимация, речь).

## Создай файл

Путь: `Code/Npcs/Layers/BaseNpcLayer.cs`

```csharp
namespace Sandbox.Npcs.Layers;

/// <summary>
/// A behavior layer provides specific services for tasks to use -- we don't use behavior layers for state, they are services.
/// </summary>
public abstract class BaseNpcLayer : Component
{
	private Npc _npc;

	/// <summary>
	/// The owning NPC
	/// </summary>
	protected Npc Npc
	{
		get
		{
			_npc ??= GetComponent<Npc>();
			return _npc;
		}
	}

	/// <summary>
	/// Optional debug string shown in the NPC debug overlay. Return null to skip.
	/// </summary>
	public virtual string GetDebugString() => null;

	/// <summary>
	/// Reset any runtime state on this layer.
	/// </summary>
	public virtual void Reset() { }
}
```

## Разбор

| Элемент | Что делает |
|---------|-----------|
| `_npc ??= GetComponent<Npc>()` | Ленивая инициализация — находит Npc на том же объекте |
| `GetDebugString()` | Вклад в отладочный оверлей (null = ничего не показывать) |
| `Reset()` | Сброс состояния (например, при смене расписания) |

---

Следующий шаг: [14.09 — NPC: Слой сенсоров (SensesLayer)](22_06_Senses_Speech.md)
