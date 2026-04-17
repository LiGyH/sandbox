# 12.02 — Компонент: Очистка констрейнтов (ConstraintCleanup) 🔗

## Что мы делаем?

Создаём **ConstraintCleanup** — компонент, который автоматически удаляет связанный объект (`Attachment`) при уничтожении самого компонента, и самоуничтожается если связанный объект уже удалён.

## Зачем это нужно?

Когда ToolGun создаёт физическое соединение (Weld, Rope, Elastic), он создаёт вспомогательные объекты. `ConstraintCleanup` следит за парой: если один объект удалён — удаляем другой. Без этого компонента при удалении пропа остались бы «висящие» невидимые объекты.

## Как это работает внутри движка?

- `OnDestroy()` — вызывается движком при уничтожении `GameObject`. Мы проверяем `Attachment.IsValid()` и удаляем его.
- `OnUpdate()` — вызывается каждый кадр. Если `Attachment` уже не валиден — уничтожаем себя через `DestroyGameObject()`.
- `DestroyGameObject()` — метод `Component`, уничтожающий `GameObject`, на котором висит этот компонент.

## Создай файл

Путь: `Code/Components/ConstraintCleanup.cs`

```csharp
﻿internal class ConstraintCleanup : Component
{
	[Property]
	public GameObject Attachment { get; set; }

	protected override void OnDestroy()
	{
		if ( Attachment.IsValid() )
		{
			Attachment.Destroy();
		}

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if (  !Attachment.IsValid() )
		{
			DestroyGameObject();
			return;
		}
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент пока не используется напрямую — он будет задействован позже при создании ToolGun (Фаза 8).


---

## ➡️ Следующий шаг

Переходи к **[12.03 — Компонент: Ручная связь (ManualLink) 🔗](12_03_ManualLink.md)**.
