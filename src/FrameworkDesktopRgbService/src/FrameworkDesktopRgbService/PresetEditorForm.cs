using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FrameworkDesktopRgbService;

public sealed class PresetEditorForm : Form
{
    private const int LedCount = 8;

    private readonly ConfigService _configService;
    private AppConfig _config;
    private readonly List<Button> _colorButtons = new();
    private readonly TextBox _nameTextBox;
    private readonly ComboBox _animationComboBox;
    private readonly ListBox _presetListBox;
    private readonly Button _deleteButton;

    public bool ConfigChanged { get; private set; }

    public PresetEditorForm(ConfigService configService)
    {
        _configService = configService;
        _config = _configService.Load();

        Text = "RGB Preset Editor";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        var mainLayout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        // Left column: preset list and actions
        var listLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        _presetListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Height = 200,
        };
        _presetListBox.SelectedIndexChanged += (_, _) => LoadSelectedPreset();

        var listButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };

        var newButton = new Button { Text = "New" };
        newButton.Click += (_, _) => CreateNewPreset();

        _deleteButton = new Button { Text = "Delete" };
        _deleteButton.Click += (_, _) => DeleteSelectedPreset();

        listButtons.Controls.Add(newButton);
        listButtons.Controls.Add(_deleteButton);

        listLayout.Controls.Add(_presetListBox, 0, 0);
        listLayout.Controls.Add(listButtons, 0, 1);

        // Right column: preset editor
        var editorLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 6,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        var nameLayout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };
        nameLayout.Controls.Add(new Label { Text = "Name:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        _nameTextBox = new TextBox { Width = 200 };
        nameLayout.Controls.Add(_nameTextBox);

        var animationLayout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };
        animationLayout.Controls.Add(new Label { Text = "Animation:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        _animationComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
        };
        _animationComboBox.Items.AddRange(new object[] { "Static", "GradientSweep", "Breathe" });
        _animationComboBox.SelectedIndex = 0;
        animationLayout.Controls.Add(_animationComboBox);

        var gradientHint = new Label
        {
            Text = "GradientSweep uses at least two non-black colors; black slots are ignored.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Padding = new Padding(0, 3, 0, 3),
        };

        var colorsLayout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 5, 0, 5),
        };

        for (var i = 0; i < LedCount; i++)
        {
            var button = new Button
            {
                Text = $"LED {i + 1}",
                Width = 80,
                Height = 40,
                BackColor = Color.Black,
                ForeColor = Color.White,
            };

            button.Click += (_, _) => PickColor(button);
            _colorButtons.Add(button);
            colorsLayout.Controls.Add(button);
        }

        var actionButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };

        var saveButton = new Button { Text = "Save" };
        saveButton.Click += (_, _) => SavePreset();

        var closeButton = new Button { Text = "Close" };
        closeButton.Click += (_, _) => Close();

        actionButtons.Controls.Add(closeButton);
        actionButtons.Controls.Add(saveButton);

        editorLayout.Controls.Add(nameLayout, 0, 0);
        editorLayout.Controls.Add(animationLayout, 0, 1);
        editorLayout.Controls.Add(gradientHint, 0, 2);
        editorLayout.Controls.Add(new Label { Text = "Pick colors for 8 LEDs:", AutoSize = true }, 0, 3);
        editorLayout.Controls.Add(colorsLayout, 0, 4);
        editorLayout.Controls.Add(actionButtons, 0, 5);

        mainLayout.Controls.Add(listLayout, 0, 0);
        mainLayout.Controls.Add(editorLayout, 1, 0);

        Controls.Add(mainLayout);

        LoadPresets();
    }

    private void LoadPresets()
    {
        _presetListBox.Items.Clear();
        foreach (var preset in _config.Presets)
        {
            _presetListBox.Items.Add(preset.Name);
        }

        if (_presetListBox.Items.Count > 0)
        {
            _presetListBox.SelectedIndex = 0;
        }
        UpdateDeleteButton();
    }

    private void LoadSelectedPreset()
    {
        if (_presetListBox.SelectedItem is not string name)
        {
            return;
        }

        var preset = _config.Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            return;
        }

        _nameTextBox.Text = preset.Name;

        var animationValue = preset.Animation ?? "Static";
        var matchingAnimation = _animationComboBox.Items
            .Cast<string>()
            .FirstOrDefault(i => string.Equals(i, animationValue, StringComparison.OrdinalIgnoreCase))
            ?? "Static";
        _animationComboBox.SelectedItem = matchingAnimation;

        for (var i = 0; i < LedCount; i++)
        {
            var colorValue = i < preset.Colors.Count ? preset.Colors[i] : "0x000000";
            var color = HexToColor(colorValue);
            _colorButtons[i].BackColor = color;
            _colorButtons[i].ForeColor = GetContrastingTextColor(color);
        }

        UpdateDeleteButton();
    }

    private void CreateNewPreset()
    {
        _nameTextBox.Text = string.Empty;
        _animationComboBox.SelectedIndex = 0;
        foreach (var button in _colorButtons)
        {
            button.BackColor = Color.Black;
            button.ForeColor = Color.White;
        }
        _presetListBox.ClearSelected();
        UpdateDeleteButton();
    }

    private void DeleteSelectedPreset()
    {
        if (_presetListBox.SelectedItem is not string name)
        {
            return;
        }

        if (_config.Presets.Count <= 1)
        {
            MessageBox.Show("At least one preset must remain.", "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var preset = _config.Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            return;
        }

        _config.Presets.Remove(preset);
        if (string.Equals(_config.LastPresetName, preset.Name, StringComparison.OrdinalIgnoreCase))
        {
            _config.LastPresetName = _config.Presets.First().Name;
        }

        _configService.Save(_config);
        ConfigChanged = true;
        LoadPresets();
        UpdateDeleteButton();
    }

    private void SavePreset()
    {
        var name = _nameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Preset name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var colors = _colorButtons.Select(b => ColorToHex(b.BackColor)).ToList();
        if (colors.Count != LedCount)
        {
            MessageBox.Show($"You must pick exactly {LedCount} colors.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var animation = (_animationComboBox.SelectedItem as string) ?? "Static";

        if (animation.Equals("GradientSweep", StringComparison.OrdinalIgnoreCase))
        {
            var nonBlackCount = colors.Count(c => HexToColor(c).ToArgb() != Color.Black.ToArgb());
            if (nonBlackCount < 2)
            {
                MessageBox.Show("GradientSweep requires at least two non-black colors.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        if (animation.Equals("Breathe", StringComparison.OrdinalIgnoreCase))
        {
            var nonBlackCount = colors.Count(c => HexToColor(c).ToArgb() != Color.Black.ToArgb());
            if (nonBlackCount < 1)
            {
                MessageBox.Show("Breathe requires at least one non-black color.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var existing = _config.Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _config.Presets.Add(new RgbPreset
            {
                Name = name,
                Colors = colors,
                Animation = animation,
            });
        }
        else
        {
            existing.Colors = colors;
            existing.Animation = animation;
        }

        if (_presetListBox.SelectedItem is null)
        {
            _config.LastPresetName = name;
        }

        _configService.Save(_config);
        ConfigChanged = true;
        LoadPresets();

        var index = _presetListBox.Items.IndexOf(name);
        if (index >= 0)
        {
            _presetListBox.SelectedIndex = index;
        }

        UpdateDeleteButton();
    }

    private static void PickColor(Button button)
    {
        using var dialog = new ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = button.BackColor,
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            button.BackColor = dialog.Color;
            button.ForeColor = GetContrastingTextColor(dialog.Color);
        }
    }

    private static Color HexToColor(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }
        else if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6 || !int.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var intColor))
        {
            return Color.Black;
        }

        var r = (intColor >> 16) & 0xFF;
        var g = (intColor >> 8) & 0xFF;
        var b = intColor & 0xFF;
        return Color.FromArgb(r, g, b);
    }

    private static string ColorToHex(Color color)
    {
        return $"0x{color.R:X2}{color.G:X2}{color.B:X2}".ToLowerInvariant();
    }

    private static Color GetContrastingTextColor(Color background)
    {
        var brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
        return brightness > 128 ? Color.Black : Color.White;
    }

    private void UpdateDeleteButton()
    {
        _deleteButton.Enabled = _config.Presets.Count > 1 && _presetListBox.SelectedItem is not null;
    }
}
