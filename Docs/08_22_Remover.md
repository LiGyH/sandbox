# 08.22 — Удалитель (Remover) 🧨

## Что мы делаем?

Создаём инструмент `Remover` — режим Tool Gun для удаления объектов из мира. Это самый простой инструмент: наводим на объект, нажимаем — объект уничтожается.

## Зачем это нужно?

Удалитель — необходимый инструмент для:
- Быстрого удаления ненужных объектов из мира
- Очистки неудачных конструкций
- Управления количеством объектов на сервере

## Как это работает внутри движка?

- **`TraceHitboxes`** — установлен в `true`, что позволяет трассировке попадать в хитбоксы, а не только в коллайдеры.
- **`CanDestroy()`** — проверяет, что объект существует и имеет тег `"removable"`. Только объекты с этим тегом могут быть удалены.
- **`OnControl()`** — при нажатии `attack1` трассирует луч, находит корневой сетевой объект и удаляет его.
- **`Remove()`** — RPC-метод, выполняющийся на хосте. Дополнительно проверяет `IsProxy` — прокси-объекты не удаляются напрямую.
- Не наследуется от `BaseConstraintToolMode` — наследует напрямую от `ToolMode`, так как не работает с двухстадийным выбором.

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Remover.cs`

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

## Проверка

- Класс `Remover` наследуется от `ToolMode` (не `BaseConstraintToolMode`).
- `TraceHitboxes = true` — расширенная трассировка для точного выбора.
- Удаляется корневой сетевой объект (`Network.RootGameObject`), а не дочерний.
- Двойная проверка безопасности: `CanDestroy()` на клиенте и на сервере.
- `IsProxy` проверка на сервере предотвращает удаление объектов, которыми управляет другой хост.
- Тег `"removable"` — объекты без этого тега защищены от удаления.
