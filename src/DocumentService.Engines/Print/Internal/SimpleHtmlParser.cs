using System.Text.RegularExpressions;

namespace DocumentService.Engines.Print.Internal;

/// <summary>
/// A deliberately minimal HTML reader used only to turn a merged print template
/// into a sequence of blocks (heading / paragraph / table) that both the PDF and
/// Word renderers can lay out. This is NOT a general HTML-to-PDF engine — it
/// understands h1-h3, p and table/tr/th/td only, which is sufficient for the
/// simple invoice/quotation/receipt templates used in this POC. A real HTML
/// renderer (or the future Certificate Designer engine) would replace this.
/// </summary>
internal static class SimpleHtmlParser
{
    private static readonly Regex BlockRegex = new(
        @"<h[1-3][^>]*>(?<h>.*?)</h[1-3]>|<p[^>]*>(?<p>.*?)</p>|<table[^>]*>(?<table>.*?)</table>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex RowRegex = new(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex CellRegex = new(@"<t[hd][^>]*>(.*?)</t[hd]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex HeaderCellRegex = new(@"<th[^>]*>", RegexOptions.IgnoreCase);
    private static readonly Regex TagRegex = new("<.*?>", RegexOptions.Singleline);

    public static List<HtmlBlock> Parse(string html)
    {
        var blocks = new List<HtmlBlock>();

        foreach (Match match in BlockRegex.Matches(html))
        {
            if (match.Groups["h"].Success)
            {
                blocks.Add(new HeadingBlock(StripTags(match.Groups["h"].Value)));
            }
            else if (match.Groups["p"].Success)
            {
                var text = StripTags(match.Groups["p"].Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(new ParagraphBlock(text));
                }
            }
            else if (match.Groups["table"].Success)
            {
                blocks.Add(ParseTable(match.Groups["table"].Value));
            }
        }

        return blocks;
    }

    private static TableBlock ParseTable(string tableHtml)
    {
        var rows = new List<List<string>>();
        var hasHeader = false;

        foreach (Match rowMatch in RowRegex.Matches(tableHtml))
        {
            var rowHtml = rowMatch.Groups[1].Value;
            var cells = CellRegex.Matches(rowHtml).Select(c => StripTags(c.Groups[1].Value)).ToList();
            if (cells.Count == 0) continue;

            if (rows.Count == 0 && HeaderCellRegex.IsMatch(rowHtml))
            {
                hasHeader = true;
            }

            rows.Add(cells);
        }

        return new TableBlock(rows, hasHeader);
    }

    private static string StripTags(string html) =>
        TagRegex.Replace(html, string.Empty).Trim().Replace("&amp;", "&").Replace("&nbsp;", " ");
}

internal abstract record HtmlBlock;
internal record HeadingBlock(string Text) : HtmlBlock;
internal record ParagraphBlock(string Text) : HtmlBlock;
internal record TableBlock(List<List<string>> Rows, bool FirstRowIsHeader) : HtmlBlock;
