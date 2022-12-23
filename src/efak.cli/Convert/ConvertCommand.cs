using System.Xml.Linq;

using Newtonsoft.Json;

using Typin;
using Typin.Attributes;
using Typin.Console;

namespace efak.cli.Convert;

public class Consts {

    #region Nested type: Extensions

    public class Extensions {

        public const string XML  = ".xml";
        public const string JSON = ".json";
    }

    #endregion

}

public class ConvertCommandBase : CommandBase {
    [CommandParameter(0, Description = "convert from")]
    public string? From { get; set; }

    [CommandParameter(1, Description = "convert to")]
    public string? To { get; set; }
}

[Command($"{Convert}", Description = "Convert xml <-> json")]
public class ConvertCommand : ConvertCommandBase, ICommand {

    public const string Convert = "convert";

    #region ICommand Members

    public async ValueTask ExecuteAsync(IConsole console) {
        if (!File.Exists(From)) {
            throw new InvalidOperationException($"Source {From} does not exist.");
        }

        var srcExt = Path.GetExtension(From);
        var dstExt = Path.GetExtension(To);

        string? json;
        XDocument? doc;
        switch (srcExt, dstExt) {
            case (Consts.Extensions.XML, Consts.Extensions.JSON):
                var xml = await File.ReadAllTextAsync(From).ConfigureAwait(false);
                doc = XDocument.Parse(xml);
                json = JsonConvert.SerializeXNode(doc, Formatting.Indented, false);
                await File.WriteAllTextAsync(To!, json).ConfigureAwait(false);
                break;
            case (Consts.Extensions.JSON, Consts.Extensions.XML):
                json = await File.ReadAllTextAsync(From).ConfigureAwait(false);
                doc = JsonConvert.DeserializeXNode(json);
                await File.WriteAllTextAsync(To!, doc!.ToString()).ConfigureAwait(false);
                break;
            default:
                console.Output.WithForegroundColor(ConsoleColor.Red,
                                                   async writer => await writer.WriteLineAsync($"Unable to convert from: {From!} to: {To!}"));
                break;
        }


    }

    #endregion

}