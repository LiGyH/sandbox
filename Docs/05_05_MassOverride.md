# 05.05 — Компонент: Переопределение массы (MassOverride) ⚖️

## Что мы делаем?

Создаём **MassOverride** — компонент, который применяет пользовательское значение массы к корневому `Rigidbody` объекта. Масса сохраняется при дупликации и синхронизируется по сети.

## Зачем это нужно?

По умолчанию масса объекта определяется его моделью. Но игроки могут захотеть сделать объект тяжелее или легче — например, чтобы создать устойчивую базу или летающую конструкцию. `MassOverride` позволяет задать произвольную массу через инструмент, и эта масса переживёт дупликацию и перезагрузку — благодаря сериализации `[Property]` и сетевой синхронизации `[Sync]`.

## Как это работает внутри движка?

- `[Property, Sync]` — свойство `Mass` сериализуется (сохраняется в дупах/сейвах) и синхронизируется по сети.
- `OnStart()` — вызывается при первом запуске компонента. Применяет массу.
- `OnEnabled()` — вызывается при каждом включении компонента. Применяет массу повторно.
- `Apply()` — находит `Rigidbody` на корневом `GameObject` и устанавливает `MassOverride`. Если `Rigidbody` нет — ничего не делает.
- `GameObject.Root` — корень иерархии. Гарантирует, что масса применяется к физическому телу, даже если `MassOverride` висит на дочернем объекте.

## Создай файл

Путь: `Code/Components/MassOverride.cs`

```csharp
/// <summary>
/// Applies a mass override to the root Rigidbody of this object's hierarchy.
/// Attach this to any GameObject to persist mass across duplication.
/// </summary>
public sealed class MassOverride : Component
{
	[Property, Sync]
	public float Mass { get; set; } = 100f;

	protected override void OnStart() => Apply();
	protected override void OnEnabled() => Apply();

	public void Apply()
	{
		var rb = GameObject.Root.GetComponent<Rigidbody>();
		if ( rb.IsValid() ) rb.MassOverride = Mass;
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент будет использоваться инструментом Mass в тул-гане (Фаза 8).
