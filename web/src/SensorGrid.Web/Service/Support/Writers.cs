// What this file is for: writes the CSV and PDF files of the exports and reports (no outside library needed).
// How it fits in: Service folder. They sit behind ICsvWriter and IPdfWriter, so the services never depend on how the files are made.
using System.Globalization;
using System.Text;

namespace SensorGrid.Service;

/// <summary>Writes CSV with quotes where needed. Cells that start with = + - @ are prefixed so spreadsheets do not run them as formulas.</summary>
public sealed class CsvWriter : ICsvWriter
{
    public byte[] Write(IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        sb.Append('﻿'); // lets Excel read UTF-8 correctly
        sb.AppendLine(string.Join(",", header.Select(Cell)));
        foreach (var row in rows) sb.AppendLine(string.Join(",", row.Select(Cell)));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Cell(string value)
    {
        // a leading minus on a plain number (like -3.5) is fine; only text that looks like a formula is neutralised
        if (value.Length > 0 && "=+@".Contains(value[0])) value = "'" + value;
        else if (value.Length > 1 && value[0] == '-' && !char.IsDigit(value[1]) && value[1] != '.') value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}

/// <summary>A part of a PDF report: a heading, a paragraph or a table.</summary>
public abstract record PdfBlock;

/// <summary>A bold heading (for example a province name).</summary>
public sealed record PdfHeading(string Text) : PdfBlock;

/// <summary>A paragraph of plain text. Long text wraps onto more lines.</summary>
public sealed record PdfText(string Text) : PdfBlock;

/// <summary>A table. Each column has a header and a width in points (the page has 762 points across). Text too long for its column is cut.</summary>
public sealed record PdfTable(IReadOnlyList<(string Header, int Width)> Columns, IReadOnlyList<IReadOnlyList<string>> Rows) : PdfBlock;

/// <summary>
/// Writes a PDF (Helvetica, A4 landscape) with headings, paragraphs and tables, on as many pages as needed. A table that runs onto a new page
/// repeats its header row there. No outside library.
/// </summary>
public sealed class PdfWriter : IPdfWriter
{
    private const double Left = 40, Right = 802, Top = 535, Bottom = 40;
    private const double TableSize = 7.5, TextSize = 8.5, HeadingSize = 11;

    public byte[] Write(string title, IEnumerable<PdfBlock> blocks)
    {
        var pages = new List<StringBuilder>();
        StringBuilder page = null!;
        double y = 0;

        void NewPage()
        {
            page = new StringBuilder();
            pages.Add(page);
            Text(page, Left, 555, 14, true, title);
            y = Top;
        }

        // starts a new page when the next part (of the given height) does not fit
        void Room(double height)
        {
            if (y - height < Bottom) NewPage();
        }

        NewPage();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case PdfHeading h:
                    Room(HeadingSize + 24);
                    y -= 10;
                    Text(page, Left, y, HeadingSize, true, h.Text);
                    y -= 16;
                    break;

                case PdfText t:
                    foreach (var line in Wrap(t.Text, Fits(Right - Left, TextSize)))
                    {
                        Room(13);
                        Text(page, Left, y, TextSize, false, line);
                        y -= 13;
                    }
                    y -= 4;
                    break;

                case PdfTable table:
                    void Header()
                    {
                        var x = Left;
                        foreach (var (head, width) in table.Columns)
                        {
                            Text(page, x, y, TableSize, true, Cut(head, Fits(width, TableSize)));
                            x += width;
                        }
                        page.Append($"0.75 G 0.6 w {Left} {Num(y - 4)} m {Right} {Num(y - 4)} l S\n");
                        y -= 14;
                    }
                    Room(14 + 12);
                    Header();
                    foreach (var row in table.Rows)
                    {
                        if (y - 12 < Bottom)
                        {
                            NewPage();
                            Header();
                        }
                        var x = Left;
                        for (var c = 0; c < table.Columns.Count; c++)
                        {
                            var width = table.Columns[c].Width;
                            Text(page, x, y, TableSize, false, Cut(c < row.Count ? row[c] : "", Fits(width, TableSize)));
                            x += width;
                        }
                        y -= 12;
                    }
                    y -= 8;
                    break;
            }
        }

        // object numbers: 1 catalog, 2 pages, 3 regular font, 4 bold font, then (page, content) pairs
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [" + string.Join(" ", pages.Select((_, i) => $"{5 + i * 2} 0 R")) + $"] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };
        for (var p = 0; p < pages.Count; p++)
        {
            var content = pages[p];
            Text(content, Left, 20, 8, false, $"Page {p + 1} of {pages.Count}");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + p * 2} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(content.ToString())} >>\nstream\n{content}endstream");
        }

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append(o.ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    /// <summary>Places one line of text with its bottom-left corner at (x, y).</summary>
    private static void Text(StringBuilder page, double x, double y, double size, bool bold, string text) =>
        page.Append($"BT /{(bold ? "F2" : "F1")} {Num(size)} Tf {Num(x)} {Num(y)} Td ({Escape(text)}) Tj ET\n");

    /// <summary>About how many characters fit in a width (Helvetica letters are about half as wide as the font size, a little more in bold).</summary>
    private static int Fits(double width, double size) => Math.Max(1, (int)((width - 6) / (size * 0.55)));

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..Math.Max(1, max - 1)] + ".";

    /// <summary>Breaks a paragraph into lines of at most max characters, at spaces.</summary>
    private static IEnumerable<string> Wrap(string text, int max)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > max)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Escapes PDF string characters and replaces anything outside Latin-1 (like the em dash) with a plain dash.</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch is '(' or ')' or '\\') sb.Append('\\').Append(ch);
            else if (ch == '—' || ch == '–') sb.Append('-');
            else if (ch > 255) sb.Append('?');
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}
