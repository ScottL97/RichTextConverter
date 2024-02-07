# RichTextConverter .NET Core

* RichTextConverter .NET Core, support for converting HTML rich text to Unity TMP rich text.
* Test using HTML rich text output by wangEditor.

## Usage

Install:

```shell
dotnet add package RichTextConverter --version 0.0.1
```

You can use the RichTextConverter with or without custom node(tag) handler:

```csharp
// custom node handler, can be a new node handler or override an already implemented node handler
public class TestNodeHandler : INodeHandler
{
    public HtmlNode Handle(HtmlNode node, HtmlDocument doc)
    {
        return node.UnWrap();
    }
}

var richText = "<test>test custom handler</test><h1>大标题</h1><h2>二级标题</h2><div>换行文本1</div><p><h3 style=\"color: rgb(225, 60, 57)\">换行文本2</h3></p><div><a href=\"www.google.com\"><span style=\"color: rgb(235, 144, 58); background-color: rgb(125, 125, 225);\">谷歌链接</span></a></div><br><h4 style=\"text-align: center\">center</h4><p style=\"text-align: start\">start</p>";
var richTextConverter = new RichTextConverter().AddNodeHandlers(new List<KeyValuePair<string, INodeHandler>>{new("test", new TestNodeHandler())});
// the second parameter is used to determine whether it is a RTL language
var unityRichText = richTextConverter.ConvertToUnityRichText(richText, "zh-CN");
Assert.Equal("test custom handler<size=2em><b>大标题</b></size=2em>\n<size=1.5em><b>二级标题</b></size=1.5em>\n换行文本1\n\n<#e13c39><size=1.2em><b>换行文本2</b></size=1.2em>\n</color><#1677ff><crlink=\"www.google.com\"><#eb903a><mark=#7d7de180>谷歌链接</mark=#7d7de180></color></crlink=\"www.google.com\"></color>\n\n<align=center><size=1em><b>center</b></size=1em>\n</align=center><align=left>start\n</align=left>",
    unityRichText);
```

In ASP.NET Core:

```xml
<ItemGroup>
    <PackageReference Include="RichTextConverter" Version="0.0.1" />
</ItemGroup>
```

```csharp
services.AddRichTextConverter();
```

```csharp
public class YourService
{
    private readonly RichTextConverter _richTextConverter;
    public YourService(RichTextConverter richTextConverter)
    {
        _richTextConverter = richTextConverter;
    }
    
    public string YourMethod()
    {
        var originHtmlText = "<h1>RichTextConverter</h1>";
        return _richTextConverter.ConvertToUnityRichText(originHtmlText, "zh");
    }
}
```