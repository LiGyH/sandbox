# 🛡️ Unbreakable — Инструмент «Неразрушимость»

## Что мы делаем?

Создаём файл `Unbreakable.cs` — инструмент, который делает объект **неразрушимым** (или возвращает обычную разрушимость).

## Зачем это нужно?

- Защита ключевых элементов конструкции от случайного разрушения
- Создание неуничтожаемых стен и барьеров
- Тестирование без необходимости пересоздавать объекты

## Как это работает внутри движка?

### Управление

| Кнопка | Действие |
|--------|----------|
| **ЛКМ** | Сделать неразрушимым |
| **ПКМ** | Вернуть разрушимость |

### Механизм

```csharp
prop.Health = unbreakable ? 0 : ( prop?.Model?.Data?.Health ?? 100 );
```

- **Неразрушимый**: `Health = 0` — движок s&box считает объект с HP=0 «бессмертным» (не получает урон)
- **Разрушимый**: `Health = Model.Data.Health` — восстанавливается стандартное HP из модели (или 100 если не указано)

### Только Prop

```csharp
var prop = select.GameObject.GetComponent<Prop>();
if ( !prop.IsValid() ) return;
```

Инструмент работает только с компонентами `Prop` (не с произвольными объектами).

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/Unbreakable.cs`

```csharp

[Icon( "🛡️" )]
[ClassName( "unbreakable" )]
[Group( "Tools" )]
public class Unbreakable : ToolMode
{
	public override string Description => "#tool.hint.unbreakable.description";
	public override string PrimaryAction => "#tool.hint.unbreakable.set";
	public override string SecondaryAction => "#tool.hint.unbreakable.unset";

	public override void OnControl()
	{
		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var prop = select.GameObject.GetComponent<Prop>();
		if ( !prop.IsValid() ) return;

		if ( Input.Pressed( "attack1" ) ) SetUnbreakable( prop, true );
		else if ( Input.Pressed( "attack2" ) ) SetUnbreakable( prop, false );
		else return;

		ShootEffects( select );
	}

	[Rpc.Host]
	private void SetUnbreakable( Prop prop, bool unbreakable )
	{
		if ( !prop.IsValid() || prop.IsProxy ) return;

		prop.Health = unbreakable ? 0 : ( prop?.Model?.Data?.Health ?? 100 );
	}
}
```

## Что проверить?

1. ЛКМ на проп → стреляем по нему → не разрушается
2. ПКМ на тот же проп → стреляем → разрушается

---

➡️ Следующий шаг: [10_12 — Trail (след)](10_12_Trail.md)
