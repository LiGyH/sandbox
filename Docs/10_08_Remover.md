# 🧨 Remover — Инструмент «Удалитель»

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём файл `Remover.cs` — инструмент, который **удаляет** объекты из мира. Наводишь — нажимаешь ЛКМ — объект исчезает.

## Зачем это нужно?

Удаление ненужных объектов — пропов, верёвок, NPC — всего, что помечено тегом `"removable"`.

## Как это работает внутри движка?

### Отличие от других инструментов

`Remover` наследуется напрямую от `ToolMode` (не от `BaseConstraintToolMode`), потому что ему нужна только **одна** точка — объект для удаления.

### Фильтрация

```csharp
bool CanDestroy( GameObject go )
{
    if ( !go.IsValid() ) return false;
    if ( !go.Tags.Contains( "removable" ) ) return false;
    return true;
}
```

Удалять можно только объекты с тегом `"removable"`. Это защищает от удаления карты, игроков и системных объектов. Все заспавненные пропы получают этот тег при создании.

### TraceHitboxes = true

```csharp
public override bool TraceHitboxes => true;
```

Remover может попадать по хитбоксам (например, для удаления NPC).

### RPC на хосте

```csharp
[Rpc.Host]
public void Remove( GameObject go )
{
    go = go?.Network?.RootGameObject;
    if ( !CanDestroy( go ) ) return;
    if ( go.IsProxy ) return;
    go.Destroy();
}
```

Удаление происходит на хосте. Дополнительная проверка `go.IsProxy` — объект должен принадлежать этому серверу.

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/Remover.cs`

```csharp
﻿﻿[Icon( "🧨" )]
[ClassName( "remover" )]
[Group( "Tools" )]
public class Remover : ToolMode
{
	public override bool TraceHitboxes => true;
	public override string Description => "#tool.hint.remover.description";
	public override string PrimaryAction => "#tool.hint.remover.remove";

	bool CanDestroy( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		if ( !go.Tags.Contains( "removable" ) ) return false;

		return true;
	}

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();
			if ( !select.IsValid() ) return;

			var target = select.GameObject?.Network?.RootGameObject;
			if ( !target.IsValid() ) return;
			if ( !CanDestroy( target ) ) return;

			Remove( target );
			ShootEffects( select );
		}
	}

	[Rpc.Host]
	public void Remove( GameObject go )
	{
		go = go?.Network?.RootGameObject;

		if ( !CanDestroy( go ) ) return;
		if ( go.IsProxy ) return;

		go.Destroy();
	}

}
```

## Что проверить?

1. Наводим на проп → ЛКМ → объект удалён
2. Наводим на карту → ничего не происходит (нет тега `removable`)
3. Эффект луча и удара при удалении

---

➡️ Следующий шаг: [10_09 — Resizer (масштаб)](10_09_Resizer.md)
