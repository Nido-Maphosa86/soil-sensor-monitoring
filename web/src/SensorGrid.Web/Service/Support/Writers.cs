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

/// <summary>Writes a plain text PDF (Helvetica, A4 landscape, many pages if needed). No outside library.</summary>
public sealed class PdfWriter : IPdfWriter
{
    private const int LinesPerPage = 34;

    public byte[] Write(string title, IEnumerable<string> lines)
    {
        var all = lines.ToList();
        var pages = new List<List<string>>();
        for (var i = 0; i < Math.Max(1, all.Count); i += LinesPerPage) pages.Add(all.Skip(i).Take(LinesPerPage).ToList());

        // object numbers: 1 catalog, 2 pages, 3 font, then (page, content) pairs
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [" + string.Join(" ", pages.Select((_, i) => $"{4 + i * 2} 0 R")) + $"] /Count {pages.Count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
        };
        for (var p = 0; p < pages.Count; p++)
        {
            var content = new StringBuilder("BT /F1 14 Tf 40 555 Td (" + Escape(title) + ") Tj ET\n");
            content.Append("BT /F1 8 Tf 40 535 Td 14 TL\n");
            foreach (var line in pages[p]) content.Append('(').Append(Escape(Trim(line))).Append(") Tj T*\n");
            content.Append("ET\n");
            content.Append($"BT /F1 8 Tf 40 20 Td (Page {p + 1} of {pages.Count}) Tj ET\n");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + p * 2} 0 R >>");
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

    private static string Trim(string line) => line.Length > 190 ? line[..190] : line;

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
