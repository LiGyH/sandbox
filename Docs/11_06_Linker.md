# Этап 11_06 — Linker (Линкер — связывание объектов)

## Что мы делаем?

Создаём инструмент `LinkerTool` — режим Tool Gun для создания связей (`ManualLink`) между двумя объектами. Линкер работает в два этапа: сначала выбираете первый объект, затем второй — между ними создаётся двусторонняя связь.

## Зачем это нужно?

Представьте, что вы привязываете невидимую нитку между двумя предметами. Физически они не соединяются (нет верёвки, пружины или сварки), но «знают» друг о друге. Это нужно для:

- **Дупликатора** — когда вы копируете конструкцию, связанные объекты копируются вместе, даже если между ними нет физических соединений.
- **Логических связей** — объекты могут реагировать друг на друга через `ManualLink`.
- **Группировки** — объединение нескольких отдельных объектов в логическую группу.

В отличие от Weld, Rope или других физических ограничений, Linker не создаёт физическое соединение — только логическую связь.

## Как это работает внутри движка?

### Наследование

```
ToolMode (базовый инструмент)
  └── BaseConstraintToolMode (двухэтапный инструмент: выбери A, затем B)
        └── LinkerTool (наш линкер)
```

`BaseConstraintToolMode` уже реализует логику двух этапов (`Stage == 0` → выберите первый объект, `Stage == 1` → выберите второй). Линкер переопределяет только специфичные методы.

### Двусторонняя связь

```
GameObject A                    GameObject B
  └── "link" (child)              └── "link" (child)
        └── ManualLink                  └── ManualLink
              Body → B/"link"                 Body → A/"link"
```

Создаются два дочерних объекта `"link"` — по одному на каждый из выбранных объектов. Каждый содержит `ManualLink`, ссылающийся на другой. Это двусторонняя связь: A знает о B, и B знает об A.

### Атрибуты класса

- **`[Icon("🔗")]`** — эмодзи-иконка в меню инструментов.
- **`[Title("Linker")]`** — отображаемое название.
- **`[ClassName("linker")]`** — CSS-класс для стилизации.
- **`[Group("Constraints")]`** — группа в меню Tool Gun (вместе с Weld, Rope и т.д.).

## Создай файл

📁 `Code/Weapons/ToolGun/Modes/Linker/LinkerTool.cs`

```csharp

[Icon( "🔗" )]
[Title( "Linker" )]
[ClassName( "linker" )]
[Group( "Constraints" )]
public class LinkerTool : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.linker.stage1" : "#tool.hint.linker.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.linker.finish" : "#tool.hint.linker.source";
	public override string ReloadAction => "#tool.hint.linker.remove";

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var link in linked.GetComponentsInChildren<ManualLink>( true ) )
			if ( linked == target || link.Body?.Root == target )
				yield return link.GameObject;
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		var go1 = new GameObject( point1.GameObject, false, "link" );
		var go2 = new GameObject( point2.GameObject, false, "link" );

		var link1 = go1.AddComponent<ManualLink>();
		var link2 = go2.AddComponent<ManualLink>();

		link1.Body = go2;
		link2.Body = go1;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Link";
		undo.Add( go1 );
	}
}
```

### Разбор ключевых частей

- **`Description`** и **`PrimaryAction`** — тексты подсказок меняются в зависимости от этапа (`Stage`). На этапе 0 — «выберите источник», на этапе 1 — «завершите связь». Строки начинаются с `#` — это ключи локализации.

- **`ReloadAction`** — клавиша R удаляет существующие связи (логика удаления реализована в `BaseConstraintToolMode`).

- **`FindConstraints(linked, target)`** — поиск существующих связей. Перебирает все `ManualLink` у объекта `linked` и возвращает те, которые указывают на `target` (через `link.Body?.Root`). Используется для удаления и проверки дубликатов.

- **`CreateConstraint(point1, point2)`** — создание связи:
  1. Создаёт два дочерних `GameObject` с именем `"link"` — по одному в каждом выбранном объекте.
  2. Добавляет компонент `ManualLink` на каждый.
  3. Связывает их перекрёстно: `link1.Body = go2`, `link2.Body = go1`.
  4. Спавнит в сети (`NetworkSpawn`) — связь видна всем игрокам.
  5. Регистрирует в системе отмены (`Undo`) — игрок может отменить действие.

- **`new GameObject( point1.GameObject, false, "link" )`** — создаёт дочерний объект: первый аргумент — родитель, `false` — не активен при создании (активируется после `NetworkSpawn`), `"link"` — имя.
