using Godot;
using System;
using System.Collections.Generic;

// A popup with a caption and a list to pick from, like Civ3's script.txt
// popups marked "#listbox", with any of the popup's "#button"s under the
// list. Picking an item, by double-clicking it or with the confirm button,
// closes the popup and calls onPick with its index; a button closes it and
// calls its own action.
public partial class ListPopup : Popup {
	private const int Width = 440;
	private const int Height = 420;

	public class Button {
		public string label;
		public Action action;

		public Button(string label, Action action) {
			this.label = label;
			this.action = action;
		}
	}

	private readonly string caption;
	private readonly List<string> items;
	private readonly int selected;
	private readonly Action<int> onPick;
	private readonly List<Button> buttons;

	public ListPopup(string caption, List<string> items, int selected, Action<int> onPick, List<Button> buttons = null) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 60);
		this.caption = caption;
		this.items = items;
		this.selected = selected;
		this.onPick = onPick;
		this.buttons = buttons ?? new();
	}

	public override void _Ready() {
		base._Ready();

		AddTexture(Width, Height);
		AddBackground(Width, Height);
		AddHeader(caption, 10);

		int listBottom = Height - 70 - (buttons.Count > 0 ? 34 : 0);
		ItemList list = new() {
			Position = new Vector2(30, 52),
			Size = new Vector2(Width - 60, listBottom - 52),
			SelectMode = ItemList.SelectModeEnum.Single,
		};
		list.AddThemeFontSizeOverride("font_size", 14);
		foreach (string item in items) {
			list.AddItem(item);
		}
		if (selected >= 0 && selected < items.Count) {
			list.Select(selected);
			list.EnsureCurrentIsVisible();
		}
		list.ItemActivated += index => Pick((int)index);
		AddChild(list);

		HBoxContainer row = new() { Position = new Vector2(30, listBottom + 8) };
		row.AddThemeConstantOverride("separation", 12);
		foreach (Button button in buttons) {
			Civ3MenuButton b = new() { Text = button.label, FontSize = 14 };
			Action action = button.action;
			b.Pressed += () => {
				GetParent<PopupOverlay>().OnHidePopup();
				action();
			};
			row.AddChild(b);
		}
		AddChild(row);

		AddConfirmButton(new Vector2(Width - 66, Height - 66), () => {
			int[] chosen = list.GetSelectedItems();
			if (chosen.Length > 0) {
				Pick(chosen[0]);
			}
		});
		AddCancelButton(new Vector2(Width - 106, Height - 66));
	}

	private void Pick(int index) {
		GetParent<PopupOverlay>().OnHidePopup();
		onPick(index);
	}
}
