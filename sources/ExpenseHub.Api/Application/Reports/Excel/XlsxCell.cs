namespace ExpenseHub.Api.Application.Reports.Excel;

/// <summary>
/// One cell of a worksheet: text, a whole number or a decimal amount. Numbers stay numeric in the file.
/// </summary>
internal sealed class XlsxCell
{
    private XlsxCell(XlsxCellKind kind, string? text, decimal number)
    {
        Kind = kind;
        Text = text;
        Number = number;
    }

    public XlsxCellKind Kind { get; }

    public string? Text { get; }

    public decimal Number { get; }

    public static XlsxCell FromText(string text)
    {
        return new XlsxCell(XlsxCellKind.Text, text, 0m);
    }

    public static XlsxCell FromHeader(string text)
    {
        return new XlsxCell(XlsxCellKind.Header, text, 0m);
    }

    public static XlsxCell FromInteger(int value)
    {
        return new XlsxCell(XlsxCellKind.Integer, null, value);
    }

    public static XlsxCell FromMoney(decimal value)
    {
        return new XlsxCell(XlsxCellKind.Money, null, value);
    }
}
