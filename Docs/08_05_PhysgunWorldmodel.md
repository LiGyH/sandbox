# 🌍 PhygunWorldmodel — Визуальные эффекты worldmodel физической пушки

## Что мы делаем?

Создаём файл `PhygunWorldmodel.cs` — компонент, управляющий визуальными эффектами модели от третьего лица (worldmodel) физической пушки: свечение частиц и точечный свет.

## Зачем это нужно?

Другие игроки видят worldmodel оружия. Эффекты на worldmodel обеспечивают визуальную обратную связь:
- Свечение частиц на кончике пушки меняет цвет при переключении режима (притягивание / физика)
- Точечный свет подсвечивает окружение в соответствующем цвете
- Плавная интерполяция цвета между режимами создаёт красивый переход

## Как это работает внутри движка?

- `OnUpdate` находит компонент `Physgun` в корневом объекте через `Components.Get<Physgun>(FindMode.EverythingInDescendants)`
- Проверяет `PullActive` для определения текущего режима
- `_tintFrac` плавно интерполируется от 0 (Phys) до 1 (Grav) через `MathX.Approach`
- Цвет вычисляется с двухэтапной интерполяцией: PhysTint → White → GravTint
- Применяется к `GlowEffect.Tint` и `GlowLight.LightColor`

## Создай файл

📄 `Code/Weapons/PhysGun/PhygunWorldmodel.cs`

```csharp
public sealed class PhygunWorldmodel : Component
{
	[Property] public ParticleEffect GlowEffect { get; set; }
	[Property] public PointLight GlowLight { get; set; }

	[Property] public Color GravTint { get; set; } = new Color( 1f, 0.8f, 0f );
	[Property] public Color PhysTint { get; set; } = new Color( 0f, 0.68333f, 1f );

	float _tintFrac;

	protected override void OnUpdate()
	{
		var physgun = GameObject.Root.Components.Get<Physgun>( FindMode.EverythingInDescendants );
		var pullActive = physgun?.PullActive ?? false;

		_tintFrac = MathX.Approach( _tintFrac, pullActive ? 1 : 0, Time.Delta * 5 );

		var tint = _tintFrac <= 0.5f
			? Color.Lerp( PhysTint, Color.White, _tintFrac * 2 )
			: Color.Lerp( Color.White, GravTint, (_tintFrac - 0.5f) * 2 );

		if ( GlowEffect is not null )
			GlowEffect.Tint = tint;

		if ( GlowLight is not null )
			GlowLight.LightColor = tint;
	}
}
```

## Проверка

- Файл `Code/Weapons/PhysGun/PhygunWorldmodel.cs` создан с классом `PhygunWorldmodel`
- Цвета по умолчанию: оранжевый для гравитации, синий для физики
- Плавная двухэтапная интерполяция цвета через белый
- Обновляются `GlowEffect` и `GlowLight`
