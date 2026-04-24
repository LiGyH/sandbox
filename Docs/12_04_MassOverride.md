# 12.04 — Компонент: Физические свойства (PhysicalProperties) ⚖️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

> ⚠️ **Изменение в исходниках:** ранее этот компонент назывался `MassOverride` и хранил только массу. В обновлении он был переименован в **`PhysicalProperties`** и теперь сохраняет сразу **массу, гравитацию и здоровье**. Старые ссылки на `MassOverride` в других файлах документации обновлены, но если вы найдёте упоминание — это про этот же файл.

## Что мы делаем?

Создаём **PhysicalProperties** — компонент, который сохраняет переопределённые физические свойства корня объекта (`Mass`, `GravityScale`, `Health`) **между дупликацией и сетевой сериализацией**. Без него все правки тулганом (Mass, Balloon и т.д.) терялись бы при копировании пропа.

## Зачем это нужно?

По умолчанию масса, множитель гравитации и здоровье берутся из модели/префаба. Но игроки изменяют их инструментами:

| Инструмент | Какое поле меняет |
|---|---|
| **Mass** | `Mass` |
| **Balloon** | `GravityScale` (отрицательный → шар летит вверх) |
| (зарезервировано) | `Health` |

Если просто записать новое значение в `Rigidbody.MassOverride` или `Rigidbody.GravityScale`, при дупликаторе или сейве оно потеряется — потому что `Rigidbody`-параметры не сериализуются как часть пропа. `PhysicalProperties` решает это: сами поля помечены `[Property, Sync]`, поэтому **переживают дупликацию, сейв и сетевую синхронизацию**, а на каждом старте/включении компонент применяет их обратно к `Rigidbody` и `Prop`.

## Как это работает внутри движка?

- `[Property, Sync]` — поля сериализуются (попадают в дупы/сейвы) и синхронизируются по сети.
- `OnStart()` / `OnEnabled()` — оба вызывают `Apply()`, чтобы значения применились и при первом старте, и при каждом включении (после хотлоада, например).
- `Apply()`:
  - находит `Rigidbody` на корне иерархии (`GameObject.Root`);
  - если `Mass > 0` — пишет в `Rigidbody.MassOverride`;
  - всегда применяет `GravityScale` (значение `1f` = обычная гравитация, `-1f` = «всплытие»);
  - если `Health > 0` и на корне есть `Prop` — задаёт `Prop.Health`.
- `Mass = 0f` означает «использовать массу модели по умолчанию» — это и значение по умолчанию для нового компонента.

## Создай файл

Путь: `Code/Components/PhysicalProperties.cs`

```csharp
/// <summary>
/// Persists physical properties (mass, gravity, health) across duplication and networking.
/// Attach this to any GameObject to ensure these values survive serialization.
/// </summary>
public sealed class PhysicalProperties : Component
{
	[Property, Sync]
	public float Mass { get; set; } = 0f;

	[Property, Sync]
	public float GravityScale { get; set; } = 1f;

	[Property, Sync]
	public float Health { get; set; } = 0f;

	protected override void OnStart() => Apply();
	protected override void OnEnabled() => Apply();

	public void Apply()
	{
		var rb = GameObject.Root.GetComponent<Rigidbody>();
		if ( rb.IsValid() )
		{
			if ( Mass > 0f ) rb.MassOverride = Mass;
			rb.GravityScale = GravityScale;
		}

		if ( Health > 0f )
		{
			var prop = GameObject.Root.GetComponent<Prop>();
			if ( prop.IsValid() ) prop.Health = Health;
		}
	}
}
```

## Кто использует этот компонент

- **[10.10 — Mass](10_10_Mass.md)** — записывает `Mass` при стрельбе из тула.
- **[11.01 — Balloon](11_01_Balloon.md)** — выставляет отрицательный `GravityScale`, чтобы шар компенсировал гравитацию.
- **[09.04 — ToolMode.Helpers (`ApplyPhysicsProperties`)](09_04_ToolMode_Helpers.md)** — при пере-спавне/дупликаторе восстанавливает `Mass` и `GravityScale` на копии.

## Проверка

После создания файла проект должен компилироваться. Создай куб → Mass-тулом задай ему массу 200 → продублируй (Duplicator) → масса должна **сохраниться** на копии. Аналогично для Balloon: гравитация на дупе шарика должна остаться отрицательной.

---

## ➡️ Следующий шаг

Переходи к **[12.05 — Компонент: Состояние морфов (MorphState) 🎭](12_05_MorphState.md)**.
