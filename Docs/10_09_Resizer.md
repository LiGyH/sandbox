# 🍄 Resizer — Инструмент «Масштабирование»

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём файл `Resizer.cs` — инструмент, который **увеличивает или уменьшает** размер объектов в реальном времени.

## Зачем это нужно?

- Создание гигантских или крошечных пропов
- Визуальные эксперименты
- Изменение масштаба конструкций

## Как это работает внутри движка?

### Управление

| Кнопка | Действие |
|--------|----------|
| **ЛКМ** (зажать) | Увеличить (+0.033 каждые 30мс) |
| **ПКМ** (зажать) | Уменьшить (-0.033 каждые 30мс) |
| **R** | Сбросить масштаб к 1.0 |

### Непрерывное действие

```csharp
TimeSince timeSinceAction = 0;
if ( timeSinceAction < 0.03f ) return;
```

В отличие от других инструментов, Resizer работает **непрерывно** — пока зажата кнопка, масштаб плавно меняется (каждые 30мс).

### RPC.Broadcast

```csharp
[Rpc.Broadcast]
private void Resize( GameObject go, float size )
```

`Rpc.Broadcast` вместо `Rpc.Host` — масштабирование применяется **на всех клиентах** одновременно, для мгновенной визуальной обратной связи.

### Ограничения

```csharp
if ( newScale.Length < 0.1f ) return;   // не меньше 0.1
if ( newScale.Length > 1000f ) return;  // не больше 1000
```

### TraceIgnoreTags = пустой

```csharp
public override IEnumerable<string> TraceIgnoreTags => [];
```

Resizer может масштабировать **любые** объекты, включая игроков (тег `"player"` не игнорируется).

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/Resizer.cs`

```csharp
﻿﻿
[Icon( "🍄" )]
[ClassName( "resizer" )]
[Group( "Tools" )]
public class Resizer : ToolMode
{
	public override IEnumerable<string> TraceIgnoreTags => [];

	public override string Description => "#tool.hint.resizer.description";
	public override string PrimaryAction => "#tool.hint.resizer.grow";
	public override string SecondaryAction => "#tool.hint.resizer.shrink";
	public override string ReloadAction => "#tool.hint.resizer.reset";

	TimeSince timeSinceAction = 0;

	public override void OnControl()
	{
		var select = TraceSelect();

		IsValidState = select.IsValid() && !select.IsWorld;
		if ( !IsValidState )
			return;

		if ( timeSinceAction < 0.03f )
			return;

		if ( Input.Down( "attack1" ) )
		{
			Resize( select.GameObject, 0.033f );
			timeSinceAction = 0;
		}
		else if ( Input.Down( "attack2" ) )
		{
			Resize( select.GameObject, -0.033f );
			timeSinceAction = 0;
		}
		else if ( Input.Pressed( "reload" ) )
		{
			ResetScale( select.GameObject );
			ShootEffects( select );
		}
	}

	[Rpc.Broadcast]
	private void ResetScale( GameObject go )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		go.WorldScale = Vector3.One;
	}

	[Rpc.Broadcast]
	private void Resize( GameObject go, float size )
	{
		if ( !go.IsValid() ) return;
		if ( go.IsProxy ) return;

		var newScale = go.WorldScale + size;
		if ( newScale.Length < 0.1f ) return;
		if ( newScale.Length > 1000f ) return;

		var scale = Vector3.Max( newScale, 0.01f );
		go.WorldScale = scale;
	}
}
```

## Что проверить?

1. Зажимаем ЛКМ на пропе → он плавно увеличивается
2. Зажимаем ПКМ → плавно уменьшается
3. R → масштаб возвращается к 1.0
4. Нельзя уменьшить меньше 0.1 или увеличить больше 1000

---

➡️ Следующий шаг: [10_10 — Mass (масса)](10_10_Mass.md)
