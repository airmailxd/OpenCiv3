shader_type canvas_item;

render_mode unshaded;

// The civ color of a unit's tint layer, which is drawn in grey shades.
// With useRamp, each grey is looked up in the civ's 16-shade ramp, as Civ3
// does with its ntp palettes (light shades take the ramp's pale, often
// hue-shifted tints, dark ones its deeper, more saturated shades); without
// it, the grey is multiplied by the flat tintColor.
uniform vec3 tintColor;
uniform bool useRamp = false;
uniform vec3 ramp[16];

// The greys of Civ3's ntp00 ramp, which the tint layers are drawn in, light to dark.
const float GREYS[16] = float[](0.984, 0.973, 0.961, 0.949, 0.937, 0.925, 0.914, 0.902,
	0.843, 0.784, 0.725, 0.667, 0.608, 0.549, 0.490, 0.490);

vec3 rampColor(float grey) {
	if (grey >= GREYS[0]) {
		return ramp[0];
	}
	for (int i = 0; i < 14; i++) {
		if (grey >= GREYS[i + 1]) {
			float t = (GREYS[i] - grey) / (GREYS[i] - GREYS[i + 1]);
			return mix(ramp[i], ramp[i + 1], t);
		}
	}
	// darker than the ramp (deep shading in rendered art): its darkest shade, darkened
	return ramp[15] * (grey / GREYS[14]);
}

void fragment() {
	if (useRamp) {
		float grey = dot(COLOR.rgb, vec3(0.299, 0.587, 0.114));
		COLOR = vec4(rampColor(grey), COLOR.a);
	} else {
		COLOR = vec4(tintColor, 1.0) * COLOR;
	}
}
