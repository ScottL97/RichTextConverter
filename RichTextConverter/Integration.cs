using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace RichTextConverter;

public static class ServiceCollectionExtensions
{
    public static void AddRichTextConverter(this IServiceCollection services, IEnumerable<KeyValuePair<string, INodeHandler>>? customHandlers=null)
    {
        var richTextConverter = new RichTextConverter();
        if (customHandlers != null)
        {
            richTextConverter.AddNodeHandlers(customHandlers);
        }
        services.AddSingleton(richTextConverter);
    }
}