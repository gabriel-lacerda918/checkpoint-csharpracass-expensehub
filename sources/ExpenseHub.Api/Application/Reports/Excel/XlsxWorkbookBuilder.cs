using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ExpenseHub.Api.Application.Reports.Excel;

/// <summary>
/// Writes a single-sheet .xlsx (Office Open XML) using only the base class library. An .xlsx is a ZIP package
/// of XML parts, so the result opens in Excel and in compatible applications without any external service.
/// </summary>
internal static class XlsxWorkbookBuilder
{
    private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

    private const int MoneyStyle = 1;
    private const int HeaderStyle = 2;

    public static byte[] Build(string sheetName, IReadOnlyList<IReadOnlyList<XlsxCell>> rows)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddPart(archive, "[Content_Types].xml", BuildContentTypes());
            AddPart(archive, "_rels/.rels", BuildPackageRelationships());
            AddPart(archive, "xl/workbook.xml", BuildWorkbook(sheetName));
            AddPart(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
            AddPart(archive, "xl/styles.xml", BuildStyles());
            AddPart(archive, "xl/worksheets/sheet1.xml", BuildSheet(rows));
        }

        return buffer.ToArray();
    }

    internal static string ColumnName(int zeroBasedIndex)
    {
        var name = new StringBuilder();
        int index = zeroBasedIndex;
        do
        {
            name.Insert(0, (char)('A' + (index % 26)));
            index = (index / 26) - 1;
        }
        while (index >= 0);

        return name.ToString();
    }

    private static void AddPart(ZipArchive archive, string path, XDocument document)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };

        using Stream stream = entry.Open();
        using var writer = XmlWriter.Create(stream, settings);
        document.Save(writer);
    }

    private static XDocument BuildContentTypes()
    {
        XNamespace ns = ContentTypesNamespace;

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ns + "Types",
                new XElement(ns + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));
    }

    private static XDocument BuildPackageRelationships()
    {
        XNamespace ns = PackageRelationshipsNamespace;

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ns + "Relationships",
                new XElement(
                    ns + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", RelationshipsNamespace + "/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))));
    }

    private static XDocument BuildWorkbook(string sheetName)
    {
        XNamespace ns = MainNamespace;
        XNamespace rel = RelationshipsNamespace;

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ns + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", RelationshipsNamespace),
                new XElement(
                    ns + "sheets",
                    new XElement(
                        ns + "sheet",
                        new XAttribute("name", sheetName),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(rel + "id", "rId1")))));
    }

    private static XDocument BuildWorkbookRelationships()
    {
        XNamespace ns = PackageRelationshipsNamespace;

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ns + "Relationships",
                new XElement(
                    ns + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", RelationshipsNamespace + "/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(
                    ns + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", RelationshipsNamespace + "/styles"),
                    new XAttribute("Target", "styles.xml"))));
    }

    private static XDocument BuildStyles()
    {
        XNamespace ns = MainNamespace;

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ns + "styleSheet",
                new XElement(
                    ns + "fonts",
                    new XAttribute("count", "2"),
                    new XElement(ns + "font", new XElement(ns + "sz", new XAttribute("val", "11")), new XElement(ns + "name", new XAttribute("val", "Calibri"))),
                    new XElement(ns + "font", new XElement(ns + "b"), new XElement(ns + "sz", new XAttribute("val", "11")), new XElement(ns + "name", new XAttribute("val", "Calibri")))),
                new XElement(
                    ns + "fills",
                    new XAttribute("count", "2"),
                    new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(
                    ns + "borders",
                    new XAttribute("count", "1"),
                    new XElement(ns + "border", new XElement(ns + "left"), new XElement(ns + "right"), new XElement(ns + "top"), new XElement(ns + "bottom"), new XElement(ns + "diagonal"))),
                new XElement(
                    ns + "cellStyleXfs",
                    new XAttribute("count", "1"),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"))),
                new XElement(
                    ns + "cellXfs",
                    new XAttribute("count", "3"),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"), new XAttribute("xfId", "0")),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "2"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"), new XAttribute("xfId", "0"), new XAttribute("applyNumberFormat", "1")),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "1"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"), new XAttribute("xfId", "0"), new XAttribute("applyFont", "1"))),
                new XElement(
                    ns + "cellStyles",
                    new XAttribute("count", "1"),
                    new XElement(ns + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", "0"), new XAttribute("builtinId", "0")))));
    }

    private static XDocument BuildSheet(IReadOnlyList<IReadOnlyList<XlsxCell>> rows)
    {
        XNamespace ns = MainNamespace;
        var sheetData = new XElement(ns + "sheetData");

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            int rowNumber = rowIndex + 1;
            var row = new XElement(ns + "row", new XAttribute("r", rowNumber.ToString(CultureInfo.InvariantCulture)));

            for (int columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                string reference = ColumnName(columnIndex) + rowNumber.ToString(CultureInfo.InvariantCulture);
                row.Add(BuildCell(ns, reference, rows[rowIndex][columnIndex]));
            }

            sheetData.Add(row);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                ns + "worksheet",
                new XElement(
                    ns + "cols",
                    new XElement(ns + "col", new XAttribute("min", "1"), new XAttribute("max", "1"), new XAttribute("width", "38"), new XAttribute("customWidth", "1")),
                    new XElement(ns + "col", new XAttribute("min", "2"), new XAttribute("max", "3"), new XAttribute("width", "18"), new XAttribute("customWidth", "1"))),
                sheetData));
    }

    private static XElement BuildCell(XNamespace ns, string reference, XlsxCell cell)
    {
        switch (cell.Kind)
        {
            case XlsxCellKind.Integer:
                return new XElement(
                    ns + "c",
                    new XAttribute("r", reference),
                    new XElement(ns + "v", cell.Number.ToString(CultureInfo.InvariantCulture)));
            case XlsxCellKind.Money:
                return new XElement(
                    ns + "c",
                    new XAttribute("r", reference),
                    new XAttribute("s", MoneyStyle.ToString(CultureInfo.InvariantCulture)),
                    new XElement(ns + "v", cell.Number.ToString(CultureInfo.InvariantCulture)));
            default:
                var text = new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), cell.Text);
                var textCell = new XElement(ns + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"));
                if (cell.Kind == XlsxCellKind.Header)
                {
                    textCell.Add(new XAttribute("s", HeaderStyle.ToString(CultureInfo.InvariantCulture)));
                }

                textCell.Add(new XElement(ns + "is", text));
                return textCell;
        }
    }
}
