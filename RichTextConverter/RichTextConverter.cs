using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RichTextConverter;

public interface INodeHandler
{
    HtmlNode Handle(HtmlNode node, HtmlDocument doc);
}

internal class ANodeHandler : INodeHandler
{
    public HtmlNode Handle(HtmlNode node, HtmlDocument doc)
    {
        // Add default color for hyperlinks
        var newNode = doc.CreateElement("#1677ff");
        node.ParentNode.InsertAfter(newNode, node);
        newNode.AppendChild(doc.CreateElement("crlink=\"" + node.Attributes["href"].Value + "\""));

        // Place the original a node under the crlink node, and then UnWrap
        node.ParentNode.RemoveChild(node);
        newNode.FirstChild.AppendChild(node);

        return node.UnWrap();
    }
}

public class AddLineNodeHandler : INodeHandler
{
    public HtmlNode Handle(HtmlNode node, HtmlDocument doc)
    {
        return node.AddNewLine(doc).UnWrap();
    }
}

public class UnWrapNodeHandler : INodeHandler
{
    public HtmlNode Handle(HtmlNode node, HtmlDocument doc)
    {
        return node.UnWrap();
    }
}

public class TransTagNodeHandler : INodeHandler
{
    private readonly FrozenDictionary<string, string> _transTag = new Dictionary<string, string>
    {
        {"em", "i"},
        {"strong", "b"}
    }.ToFrozenDictionary();

    public HtmlNode Handle(HtmlNode node, HtmlDocument doc)
    {
        node.Name = _transTag[node.Name];
        return node;
    }
}

public class HNodeHandler : INodeHandler
{
    private readonly FrozenDictionary<string, string> _hSize = new Dictionary<string, string>
    {
        {"h1", "size=2em"},
        {"h2", "size=1.5em"},
        {"h3", "size=1.2em"},
        {"h4", "size=1em"},
        {"h5", "size=0.8em"}
    }.ToFrozenDictionary();

    public HtmlNode Handle(HtmlNode node, HtmlDocument doc)
    {
        var newNode = doc.CreateElement(_hSize[node.Name]); // <size=xem></size=xem>
        node.ParentNode.InsertAfter(newNode, node); // <div><hx>hx</hx><size=xem></size=xem></div>
        newNode.AppendChild(doc.CreateElement("b")); // <div><hx>hx</hx><size=xem><b></b></size=xem></div>
        newNode.AddNewLine(doc); // <div><hx>hx</hx><size=xem><b></b></size=xem>\n</div>

        // Place the original hX node under the b node, and then UnWrap
        node.ParentNode.RemoveChild(node); // <div><size=xem><b></b></size=xem>\n</div>
        newNode.FirstChild.AppendChild(node); // <div><size=xem><b><hx>hx</hx></b></size=xem>\n</div>

        return node.UnWrap(); // <div><size=xem><b>hx</b></size=xem>\n</div>
    }
}

public class RichTextConverter
{
    private FrozenDictionary<string, INodeHandler> _nodeHandlers = new Dictionary<string, INodeHandler>
    {
        {"a", new ANodeHandler()},
        {"p", new AddLineNodeHandler()},
        {"div", new AddLineNodeHandler()},
        {"br", new AddLineNodeHandler()},
        {"span", new UnWrapNodeHandler()},

        {"em", new TransTagNodeHandler()},
        {"strong", new TransTagNodeHandler()},
        {"h", new HNodeHandler()}
    }.ToFrozenDictionary();

    private readonly FrozenSet<string> _leafNodes = new HashSet<string> { "#text", "br" }.ToFrozenSet();

    private readonly FrozenDictionary<string, string> _textAlignTransTag = new Dictionary<string, string>
    {
        {"center", "align=center"},
        {"left", "align=left"},
        {"start", "align=left"},
        {"right", "align=right"},
        {"justify", "align=justified"}
    }.ToFrozenDictionary();

    private readonly FrozenDictionary<string, string> _rtlTextAlignTransTag = new Dictionary<string, string>
    {
        {"center", "align=center"},
        {"left", "align=left"},
        {"start", "align=right"},
        {"right", "align=right"},
        {"justify", "align=justified"}
    }.ToFrozenDictionary();

    private readonly FrozenSet<string> _noTransTag = new HashSet<string>
    {
        // Same as HTML tag
        "u",
        "sup",
        "sub",
        // Unity’s unique rich text tags
        "align=center",
        "align=left",
        "align=right",
        "align=justified",
        "b",
        "i"
    }.ToFrozenSet();

    private readonly FrozenSet<string> _unityTagPrefix = new HashSet<string>
    {
        "line-height=",
        "mark=",
        "indent=",
        "size=",
        "crlink="
    }.ToFrozenSet();

    private readonly FrozenSet<string> _unsupportedHtmlTag = new HashSet<string>
    {
        "pre",
        "code",
        "li",
        "ul",
        "ol",
        "table",
        "tbody",
        "th",
        "tr",
        "td",
        "input",
        "hr",
        "img"
    }.ToFrozenSet();

    private readonly FrozenSet<string> _unsupportedUnityTagPrefix = new HashSet<string>
    {
        "font="
    }.ToFrozenSet();

    private readonly Regex _hTagPattern = new (@"^h[1-5]{1}$");

    private readonly Regex _colorStartTagPattern = new (@"^#[a-zA-Z0-9]{6,8}$");
    private readonly Regex _colorEndTagPattern = new (@"</#[a-zA-Z0-9]{6,8}>");
    private const string ColorEndTag = "</color>";

    private const string Nbsp = "&nbsp;";
    private const string Blank = " ";

    /// <summary>
    /// Convert HTML rich text to Unity TMP rich text
    /// </summary>
    /// <param name="html">origin HTML rich text</param>
    /// <param name="lang">the language of rich text, used to determine whether it is a RTL language</param>
    public string ConvertToUnityRichText(string html, string lang)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var nodes = htmlDoc.DocumentNode.ChildNodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            HandleNode(nodes[i], htmlDoc, lang);
        }

        var output = htmlDoc.DocumentNode.OuterHtml;
        output = output.Replace(Nbsp, Blank);
        output = _colorEndTagPattern.Replace(output, ColorEndTag);

        return output;
    }

    public RichTextConverter AddNodeHandlers(IEnumerable<KeyValuePair<string, INodeHandler>> handlers)
    {
        var nodeHandlers = _nodeHandlers
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var kv in handlers)
        {
            if (!nodeHandlers.TryAdd(kv.Key, kv.Value))
            {
                // Existing NodeHandler supports overriding
                nodeHandlers[kv.Key] = kv.Value;
            }
        }

        _nodeHandlers = nodeHandlers.ToFrozenDictionary();
        return this;
    }

    private void HandleNode(HtmlNode? node, HtmlDocument doc, string lang)
    {
        if (node == null)
        {
            throw new RichTextException("HTML node is empty");
        }
        if (_unsupportedHtmlTag.Contains(node.Name))
        {
            throw new RichTextException("Not currently supported HTML tag " + node.Name, node.OuterHtml);
        }
        if (_unsupportedUnityTagPrefix.Contains(FindTagPrefix(node.Name)))
        {
            throw new RichTextException("Not currently supported Unity TMP tag " + node.Name, node.OuterHtml);
        }

        node = HandleStyle(node, doc, lang);
        if (_nodeHandlers.TryGetValue(node.Name, out var handler))
        {
            node = handler.Handle(node, doc);
        }
        else if (_hTagPattern.Match(node.Name).Success)
        {
            node = _nodeHandlers["h"].Handle(node, doc);
        }

        if (_leafNodes.Contains(node.Name))
        {
            return;
        }

        // Ignore tags that do not need to be processed, and the rest are tags that cannot be processed
        if (!_noTransTag.Contains(node.Name) && !_unityTagPrefix.Contains(FindTagPrefix(node.Name)) && !_colorStartTagPattern.Match(node.Name).Success)
        {
            throw new RichTextException("Tag unable to handle " + node.Name, node.OuterHtml);
        }

        for (int i = 0; i < node.ChildNodes.Count; i++)
        {
            HandleNode(node.ChildNodes[i], doc, lang);
        }
    }

    private HtmlNode HandleStyle(HtmlNode node, HtmlDocument doc, string lang)
    {
        string styleAttr = node.GetAttributeValue("style", "");
        if (styleAttr == null)
        {
            return node;
        }
        var styles = styleAttr.Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var style in styles)
        {
            if (string.IsNullOrWhiteSpace(style)) continue;

            // Separate the attribute name and the attribute value
            var keyValue = style.Split(new [] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length != 2)
            {
                throw new RichTextException("Style attribute format error", style);
            }

            var attributeName = keyValue[0].Trim();
            var attributeValue = keyValue[1].Trim();

            HtmlNode newNode;
            if (attributeName == "text-align")
            {
                var textAlignTransTag = IsRtlLang(lang) ? _rtlTextAlignTransTag : _textAlignTransTag;
                if (textAlignTransTag.TryGetValue(attributeValue, out var textAlignTag))
                {
                    newNode = doc.CreateElement(textAlignTag);
                }
                else
                {
                    throw new RichTextException("Wrong text-align value", attributeValue);
                }
            }
            else if (attributeName == "text-indent")
            {
                newNode = doc.CreateElement("indent=2em");
            }
            else if (attributeName == "line-height")
            {
                newNode = doc.CreateElement("line-height=" + attributeValue + "em");
            }
            else if (attributeName == "background-color")
            {
                newNode = doc.CreateElement("mark=" + ConvertRgbToHex(attributeValue) + "80");
            }
            else if (attributeName == "color")
            {
                newNode = doc.CreateElement(ConvertRgbToHex(attributeValue));
            }
            else if (attributeName == "font-size")
            {
                newNode = doc.CreateElement("size=" + attributeValue);
            }
            else if (attributeName == "font-family")
            {
                // newNode = doc.CreateElement("font=" + attributeValue);
                throw new RichTextException("Font settings are not supported yet", attributeValue);
            }
            else
            {
                throw new RichTextException("Unsupported style", attributeName);
            }
            node.ParentNode.InsertAfter(newNode, node);
            newNode.ParentNode.RemoveChild(node);
            newNode.AppendChild(node);
        }

        return node;
    }

    private readonly FrozenSet<string> _rtlLangs = new HashSet<string>
    {
        "ar", // Arabic
        "fa", // Persian
        "iw", // Hebrew
        "ur", // Urdu
        "ug" // Uighur
    }.ToFrozenSet();

    private bool IsRtlLang(string lang)
    {
        var prefix = lang[..2];
        return _rtlLangs.Contains(prefix);
    }

    private string FindTagPrefix(string tag)
    {
        var index = tag.IndexOf('=');
        if (index != -1)
        {
            return tag.Substring(0, index + 1);
        }

        return "";
    }

    private string ConvertStringToHex(string s)
    {
        if (!int.TryParse(s, out var number))
        {
            throw new RichTextException("Rgb value parsing failed", s);
        }

        var hex = number.ToString("X");
        if (hex.Length == 1)
        {
            hex = "0" + hex;
        }

        return hex;
    }

    private string ConvertRgbToHex(string rgb)
    {
        Match match = Regex.Match(rgb, @"\((.*?),(.*?),(.*?)\)");
        if (!match.Success)
        {
            throw new RichTextException("Rgb value parsing failed", rgb);
        }

        var rgbs = match.Groups.Values.Select(g => g.Value).ToArray()[1..];
        if (rgbs.Length != 3)
        {
            throw new RichTextException("Rgb value parsing failed", rgb);
        }

        return "#" + string.Join("", rgbs.Select(r => ConvertStringToHex(r.Trim())));
    }
}

public static class HtmlNodeExtension
{
    // Note: After using this method, the original node will be deleted, all child nodes of the node will be promoted one level, and then the node will be deleted and node.ParentNode will be returned
    public static HtmlNode UnWrap(this HtmlNode node)
    {
        foreach (var childNode in node.ChildNodes.Reverse())
        {
            node.ParentNode.InsertAfter(childNode, node);
        }

        var oldNode = node;
        node = node.ParentNode;
        oldNode.Remove();

        return node;
    }

    public static void RemoveFromParent(this HtmlNode node)
    {
        node.ParentNode.RemoveChild(node);
        node.Remove();
    }

    public static HtmlNode AddNewLine(this HtmlNode node, HtmlDocument doc)
    {
        node.ParentNode.InsertAfter(doc.CreateTextNode("\n"), node);
        return node;
    }
}
