using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace YtDlpQueueDesktop.Controls;

public class DataGridViewProgressCell : DataGridViewTextBoxCell
{
    private static readonly Color TrackColor = Color.FromArgb(40, 45, 53);
    private static readonly Color FillColor = Color.FromArgb(69, 214, 192);

    public DataGridViewProgressCell()
    {
        ValueType = typeof(double);
    }

    public override object Clone()
    {
        var clone = (DataGridViewProgressCell)base.Clone();
        return clone;
    }

    protected override void Paint(
        Graphics graphics,
        Rectangle clipBounds,
        Rectangle cellBounds,
        int rowIndex,
        DataGridViewElementStates elementState,
        object? value,
        object? formattedValue,
        string? errorText,
        DataGridViewCellStyle cellStyle,
        DataGridViewAdvancedBorderStyle advancedBorderStyle,
        DataGridViewPaintParts paintParts)
    {
        double percent = 0;
        if (value is double d)
            percent = d;
        else if (value is not null)
            double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out percent);

        percent = Math.Clamp(percent, 0, 100);

        if ((paintParts & DataGridViewPaintParts.Background) == DataGridViewPaintParts.Background)
        {
            var backColor = (elementState & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected
                ? cellStyle.SelectionBackColor
                : cellStyle.BackColor;
            using var backBrush = new SolidBrush(backColor);
            graphics.FillRectangle(backBrush, cellBounds);
        }

        var barRect = new Rectangle(
            cellBounds.X + 4,
            cellBounds.Y + (cellBounds.Height / 2) - 5,
            (int)((cellBounds.Width - 8) * 0.62),
            10);

        using var trackBrush = new SolidBrush(TrackColor);
        graphics.FillRectangle(trackBrush, barRect);

        if (percent > 0 && barRect.Width > 0)
        {
            var fillWidth = (int)(barRect.Width * (percent / 100.0));
            var fillRect = new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height);
            using var fillBrush = new SolidBrush(FillColor);
            graphics.FillRectangle(fillBrush, fillRect);
        }

        var text = percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        var textColor = (elementState & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected
            ? cellStyle.SelectionForeColor
            : cellStyle.ForeColor;
        using var textBrush = new SolidBrush(textColor);

        var textSize = graphics.MeasureString(text, cellStyle.Font);
        var textPos = new PointF(
            barRect.Right + 8,
            cellBounds.Y + (cellBounds.Height - textSize.Height) / 2);
        graphics.DrawString(text, cellStyle.Font, textBrush, textPos);

        if ((paintParts & DataGridViewPaintParts.Border) == DataGridViewPaintParts.Border)
        {
            PaintBorder(graphics, clipBounds, cellBounds, cellStyle, advancedBorderStyle);
        }
    }
}
