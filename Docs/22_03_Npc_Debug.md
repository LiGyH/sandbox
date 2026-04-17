# 22.03 — NPC: Отладочный оверлей (Npc.Debug) 🐛

## Что мы делаем?

Добавляем **Npc.Debug.cs** — partial-часть, которая рисует отладочную информацию над NPC (текущее расписание, задача, данные слоёв).

## Создай файл

Путь: `Code/Npcs/Npc.Debug.cs`

```csharp
﻿using Sandbox.Npcs.Layers;

namespace Sandbox.Npcs;

public partial class Npc : Component
{
	public void DrawDebugString()
	{
		var bounds = GameObject.GetBounds();
		var worldpos = WorldPosition - (Vector3.Up * -bounds.Maxs.z);
		var pos = Scene.Camera.PointToScreenPixels( worldpos, out var behind );
		if ( behind ) return;

		var str = $"{ActiveSchedule?.GetDebugString()}";

		// Collect debug output from all layers
		foreach ( var layer in GetComponents<BaseNpcLayer>() )
		{
			var layerDebug = layer.GetDebugString();
			if ( !string.IsNullOrEmpty( layerDebug ) )
			{
				str += $"\n{layerDebug}";
			}
		}

		var text = TextRendering.Scope.Default;
		text.Text = str;
		text.FontSize = 13;
		text.FontName = "Poppins";
		text.FontWeight = 600;
		text.TextColor = Color.Yellow;
		text.Outline = new TextRendering.Outline { Color = Color.Black, Size = 4, Enabled = true };
		text.FilterMode = Rendering.FilterMode.Point;

		DebugOverlay.ScreenText( pos, text, TextFlag.LeftBottom );
	}
}
```

## Как включить

В инспекторе NPC установить `ShowDebugOverlay = true`. Текст рисуется над головой NPC жёлтым шрифтом с чёрной обводкой.

---

Следующий шаг: [14.03 — NPC: Ссылки на слои (Npc.Layers)](14_03_NpcLayers.md)
