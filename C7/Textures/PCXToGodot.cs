using Godot;
using System;
using ConvertCiv3Media;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class PCXToGodot : GodotObject {
	private const byte SHADOW_START_INDEX = 224;
	private const byte CIVCOLOR_START_INDEX = 239;
	private const byte TRANSPARENCY_START_INDEX = 254;
	private const byte SHADOW_INDEX_RANGE = CIVCOLOR_START_INDEX - SHADOW_START_INDEX;
	private const ushort MAX_PALETTE_SIZE = 256;

	private const byte GREEN_BITSHIFT = 8;
	private const byte BLUE_BITSHIFT = 16;
	private const byte ALPHA_BITSHIFT = 24;
	private const byte MAX_COLOR = 255;


	public struct ColorOptions {
		public static readonly ColorOptions Default = new();

		// The set of color indexes considered transparent when loading a Civ3 PCX
		public HashSet<int> transparentColorIndexes = [254, 255];

		public bool shadows = true;

		public ColorOptions(bool shadows, HashSet<int> transparentColorIndexes) {
			this.shadows = shadows;
			this.transparentColorIndexes = transparentColorIndexes;
		}

		public ColorOptions(bool shadows) {
			this.shadows = shadows;
		}

		public ColorOptions() { }

		// Implicit conversion to initialize ColorOptions with a custom 'shadows' value
		public static implicit operator ColorOptions(bool value) => new(value);
	}

	public static ImageTexture getImageTextureFromPCX(Pcx pcx) {
		Image ImgTxtr = ByteArrayToImage(pcx.ColorIndices, pcx.Palette, pcx.Width, pcx.Height);
		return getImageTextureFromImage(ImgTxtr);
	}

	// Like getImageTextureFromPCX(pcx), but with the given color options instead of the defaults.
	public static ImageTexture getImageTextureFromPCX(Pcx pcx, ColorOptions colorOptions) {
		return getImageTextureFromPCX(pcx, new CropRegion(0, 0, pcx.Width, pcx.Height), colorOptions);
	}

	public static ImageTexture getImageTextureFromPCX(Pcx pcx, CropRegion cropRegion, ColorOptions colorOptions) {
		Image image = getImageFromPCX(pcx, cropRegion, colorOptions);
		return getImageTextureFromImage(image);
	}

	/**
	 * This method is for cases where we want to use components of multiple PCXs in a texture, such as for the popup background.
	 **/
	public static Image getImageFromPCX(Pcx pcx, CropRegion cropRegion, ColorOptions colorOptions) {
		var (leftStart, topStart, croppedWidth, croppedHeight) = cropRegion;

		int[] ColorData = loadPalette(pcx.Palette, colorOptions);
		byte[] Data = new byte[4 * croppedWidth * croppedHeight];
		Span<int> BufferData = MemoryMarshal.Cast<byte, int>(Data.AsSpan());

		int DataIndex = 0;

		for (int y = topStart; y < topStart + croppedHeight; y++) {
			for (int x = leftStart; x < leftStart + croppedWidth; x++) {
				BufferData[DataIndex] = ColorData[pcx.ColorIndexAt(x, y)];
				DataIndex++;
			}
		}

		return getImageFromBufferData(croppedWidth, croppedHeight, Data);
	}

	public static ImageTexture getPureAlphaFromPCX(Pcx alphaPcx, HashSet<int> transparentColorIndexes) {
		byte[] data = new byte[4 * alphaPcx.Width * alphaPcx.Height];
		Span<int> bufferData = MemoryMarshal.Cast<byte, int>(data.AsSpan());
		int[] alphaData = new int[MAX_PALETTE_SIZE];
		for (int i = 0; i < MAX_PALETTE_SIZE; i++) {
			alphaData[i] = alphaPcx.Palette[i, 0];
		}
		int dataIndex = 0;
		for (int y = 0; y < alphaPcx.Height; y++) {
			for (int x = 0; x < alphaPcx.Width; x++, dataIndex++) {
				int index = alphaPcx.ColorIndexAt(x, y);
				// While we might want to treat the image as pure alpha
				// there are cases like the FogOfWar texture that has some extra pixels
				// that were probably used for alignment, that we don't want to render as opaque
				// since they are producing ugly dark dots in every tile.
				// This option allows us to use any transparentColorIndexes that we define,
				// or the default ones from ColorOptions along with the pure alpha option
				// Just to make the point as clear as I can, if we really want to use ONLY the pure_alpha option
				// without excluding any indexes, we would have to do this in lua
				// transparent_color_indexes = {}
				// since if we don't define even an empty table, the default indexes are still [254, 255]
				if (transparentColorIndexes.Contains(index)) {
					bufferData[dataIndex] = 0;
					continue;
				}
				bufferData[dataIndex] = (255 - alphaData[index]) << 24;
			}
		}

		Image outImage = getImageFromBufferData(alphaPcx.Width, alphaPcx.Height, data);
		return getImageTextureFromImage(outImage);
	}

	public static ImageTexture getImageFromPCXWithAlphaBlend(Pcx imagePcx, Pcx alphaPcx) {
		return getImageFromPCXWithAlphaBlend(imagePcx, alphaPcx, new(0, 0, imagePcx.Width, imagePcx.Height));
	}

	//Combines two PCXs, one used for the alpha, to produce a final output image.
	//Some files, such as Art/interface/menuButtons.pcx and Art/interface/menuButtonsAlpha.pcx, use this method.
	public static ImageTexture getImageFromPCXWithAlphaBlend(Pcx imagePcx, Pcx alphaPcx, CropRegion cropRegion, int alphaRowOffset = 0) {
		var (leftStart, topStart, croppedWidth, croppedHeight) = cropRegion;
		int[] ColorData = loadPalette(imagePcx.Palette, false);
		int[] AlphaData = loadAlphaPalette(alphaPcx.Palette, ColorData);
		byte[] Data = new byte[4 * croppedWidth * croppedHeight];
		Span<int> BufferData = MemoryMarshal.Cast<byte, int>(Data.AsSpan());

		int AlphaIndex;
		int DataIndex = 0;

		for (int y = topStart; y < topStart + croppedHeight; y++) {
			AlphaIndex = (y - alphaRowOffset) * imagePcx.Width + leftStart;
			for (int x = leftStart; x < leftStart + croppedWidth; x++) {
				BufferData[DataIndex] = ColorData[imagePcx.ColorIndexAt(x, y)] | AlphaData[alphaPcx.ColorIndices[AlphaIndex]];
				DataIndex++;
				AlphaIndex++;
			}
		}

		Image OutImage = getImageFromBufferData(croppedWidth, croppedHeight, Data);
		return getImageTextureFromImage(OutImage);
	}

	public static Image ByteArrayToImage(byte[] colorIndices, byte[,] palette, int width, int height, int[] transparent = null, bool shadows = false) {
		int[] ColorData = loadPalette(palette, shadows);
		byte[] Data = new byte[4 * width * height];
		Span<int> BufferData = MemoryMarshal.Cast<byte, int>(Data.AsSpan());

		for (int i = 0; i < width * height; i++) {
			BufferData[i] = ColorData[colorIndices[i]];
		}

		return getImageFromBufferData(width, height, Data);
	}

	// ByteArrayWithTintToImage is used to load create images from flic frames
	// that contain a tinted layer such as unit animations, where the unit's
	// clothing is tinted by their civ color.
	public static (Image, Image) ByteArrayWithTintToImage(byte[] colorIndices, byte[,] palette, int width, int height, int[] transparent = null, bool shadows = false) {
		int[] colorData = loadFlicPalette(palette, shadows);
		int[] whiteColorData = loadWhitePalette();
		byte[] baseData = new byte[4 * width * height];
		byte[] tintData = new byte[4 * width * height];
		// Both layers start out transparent.
		Span<int> baseLayer = MemoryMarshal.Cast<byte, int>(baseData.AsSpan());
		Span<int> tintLayer = MemoryMarshal.Cast<byte, int>(tintData.AsSpan());

		for (int i = 0; i < width * height; i++) {
			int index = colorIndices[i];
			bool tinted = index < 16 || (index < 64 && index % 2 == 0);
			bool shadow = index >= SHADOW_START_INDEX && index <= CIVCOLOR_START_INDEX;
			if (tinted) {
				tintLayer[i] = whiteColorData[index];
			} else if (shadow) {
				// shadow belongs to the base texture
				baseLayer[i] = shadowColors[index - SHADOW_START_INDEX];
			} else {
				baseLayer[i] = colorData[index];
			}
		}
		return (getImageFromBufferData(width, height, baseData), getImageFromBufferData(width, height, tintData));
	}

	// The colors of the shadow indexes in flic frames: white, increasingly opaque.
	private static readonly int[] shadowColors = makeShadowColors();

	private static int[] makeShadowColors() {
		int[] colors = new int[CIVCOLOR_START_INDEX - SHADOW_START_INDEX + 1];
		for (int index = SHADOW_START_INDEX; index <= CIVCOLOR_START_INDEX; index++) {
			colors[index - SHADOW_START_INDEX] = (int)new Color(1.0f, 1.0f, 1.0f, (float)(index - SHADOW_START_INDEX) / SHADOW_INDEX_RANGE).ToArgb32();
		}
		return colors;
	}

	// The palette for the tinted layer of flic frames, loaded once. If
	// ntp00.pcx doesn't exist, don't try to match the full palette for the
	// "clothes" of the unit's animation - just make it solid white. We lose any
	// sort of shading or shadows on the unit's clothing, but this is good
	// enough for standalone mode, at least for now.
	//
	// TODO: consider how to improve this - do we need to include a similar
	// palette in the c7 data? Is this information present in the flc files?
	private static int[] whitePalette = null;

	private static int[] loadWhitePalette() {
		if (whitePalette == null) {
			try {
				Pcx whitePcx = TextureLoader.LoadPCX("Art/Units/Palettes/ntp00.pcx");
				whitePalette = loadPalette(whitePcx.Palette, true);
			} catch (Exception) {
				whitePalette = new int[MAX_PALETTE_SIZE];
				Array.Fill(whitePalette, (int)new Color(1, 1, 1, 1).ToArgb32());
			}
		}
		return whitePalette;
	}

	// All frames of a flic share its palette, so the colors made from the last
	// palette are kept for the next frame.
	private static byte[,] lastFlicPalette = null;
	private static bool lastFlicShadows;
	private static int[] lastFlicColorData = null;

	private static int[] loadFlicPalette(byte[,] palette, bool shadows) {
		if (lastFlicPalette != palette || lastFlicShadows != shadows) {
			lastFlicColorData = loadPalette(palette, shadows);
			lastFlicPalette = palette;
			lastFlicShadows = shadows;
		}
		return lastFlicColorData;
	}

	// Forgets the palettes loaded from files, for when the media files change.
	public static void ClearCache() {
		whitePalette = null;
		lastFlicPalette = null;
		lastFlicColorData = null;
	}

	// Makes an image from RGBA8 pixel data.
	private static Image getImageFromBufferData(int width, int height, byte[] data) {
		return Image.CreateFromData(width, height, false, Image.Format.Rgba8, data);
	}

	private static ImageTexture getImageTextureFromImage(Image image) {
		return ImageTexture.CreateFromImage(image);
	}

	private static int[] loadPalette(byte[,] palette, ColorOptions colorOptions) {
		int Red, Green, Blue;
		int[] ColorData = new int[MAX_PALETTE_SIZE];

		for (int i = 0; i < MAX_PALETTE_SIZE; i++) {
			Red = palette[i, 0];
			Green = palette[i, 1] << GREEN_BITSHIFT;
			Blue = palette[i, 2] << BLUE_BITSHIFT;

			int Alpha = colorOptions.transparentColorIndexes.Contains(i) ? 0 : MAX_COLOR << ALPHA_BITSHIFT;

			ColorData[i] = Red + Green + Blue + Alpha;
		}

		if (colorOptions.shadows) {
			for (int i = 240; i < 256; i++) {
				ColorData[i] = ((MAX_COLOR - i) * 16) << ALPHA_BITSHIFT;
			}
		}

		return ColorData;
	}

	private static int[] loadAlphaPalette(byte[,] palette, int[] ColorData) {
		int[] AlphaData = new int[MAX_PALETTE_SIZE];

		for (int i = 0; i < MAX_PALETTE_SIZE; i++) {
			// Assumption based on menuButtonsAlpha.pcx: The palette in the alpha PCX always has the same red, green, and blue values (i.e. is grayscale).
			// Examining it with breakpoints in my Java code, it appears it starts at 255, 255, 255, and goes down one at a time.  But this code
			// doesn't assume that, it only assumes the grayscale aspect.  In theory, this should work for any transparency, 0 to 255.
			AlphaData[i] = palette[i, 0] << 24;
			ColorData[i] = ColorData[i] &= 0x00ffffff;
		}

		return AlphaData;
	}
}
