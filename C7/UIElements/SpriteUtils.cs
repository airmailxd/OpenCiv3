using C7.Textures;
using C7GameData;
using Godot;

public static class SpriteUtils {
	public static (Texture2D baseFrame, Texture2D tintFrame, ShaderMaterial material) GetUnitTextures(Game game, MapUnit unit) {
		var am = game.animationController.civ3AnimData;

		var (baseFrame, tintFrame) = am.GetAnimationFrameAndTintTextures(unit);

		var color = unit.owner.GetPlayerColor();
		ShaderMaterial material = PlayerTextureUtil.GetShaderMaterialForUnit(color);
		return (baseFrame, tintFrame, material);
	}

	public static (Sprite2D unitSprite, Sprite2D unitTintSprite) GetUnitSprites(Game game, MapUnit unit) {
		var (baseFrame, tintFrame, material) = GetUnitTextures(game, unit);

		// Add the base sprite.
		var unitSprite = new Sprite2D();
		unitSprite.Texture = baseFrame;

		// Add the tint sprite, hooking up the shader.
		var unitTintSprite = new Sprite2D();
		unitTintSprite.Texture = tintFrame;
		unitTintSprite.Material = material;

		return (unitSprite, unitTintSprite);
	}
}
