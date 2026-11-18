namespace PixelArt.App;

// Один небольшой диалог для размеров холста, масштаба экспорта и сдвига.
public sealed class NumberDialog : Form
{
    private readonly NumericUpDown first;
    private readonly NumericUpDown? second;
    public int First => (int)first.Value;
    public int Second => (int)(second?.Value ?? 0);

    public NumberDialog(string title, string firstLabel, int firstValue, int minimum, int maximum,
        string? secondLabel = null, int secondValue = 0)
    {
        Text = title;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var layout = new TableLayoutPanel { AutoSize = true, Padding = new Padding(20), ColumnCount = 2 };
        Controls.Add(layout);
        first = AddNumber(layout, firstLabel, firstValue, minimum, maximum);
        if (secondLabel != null) second = AddNumber(layout, secondLabel, secondValue, minimum, maximum);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([ok, cancel]);
        layout.Controls.Add(buttons, 0, layout.RowCount++);
        layout.SetColumnSpan(buttons, 2);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static NumericUpDown AddNumber(TableLayoutPanel layout, string label, int value, int min, int max)
    {
        int row = layout.RowCount++;
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 16, 12) }, 0, row);
        var input = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = 130 };
        layout.Controls.Add(input, 1, row);
        return input;
    }
}
