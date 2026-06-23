using System.Windows.Forms;

namespace YtDlpQueueDesktop.Controls;

/// <summary>
/// Kolomtype voor <see cref="DataGridViewProgressCell"/>. Het celwaarde-type is double
/// (0-100), gebruik dus <c>row.Cells[col].Value = 42.5;</c>.
/// </summary>
public class DataGridViewProgressColumn : DataGridViewColumn
{
    public DataGridViewProgressColumn() : base(new DataGridViewProgressCell())
    {
    }

    public override DataGridViewCell? CellTemplate
    {
        get => base.CellTemplate;
        set
        {
            if (value is not null && value is not DataGridViewProgressCell)
                throw new InvalidCastException("CellTemplate moet van het type DataGridViewProgressCell zijn.");
            base.CellTemplate = value;
        }
    }
}
