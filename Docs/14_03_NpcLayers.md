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

Следующий шаг: [14.04 — NPC: Планировщик расписаний (Npc.Schedule)](14_04_NpcSchedule.md)
