using System.Xml;
using System.Xml.Serialization;

using Newtonsoft.Json;

using Typin;
using Typin.Attributes;
using Typin.Console;

using UblSharp;

using Formatting = Newtonsoft.Json.Formatting;

namespace efak.cli.Convert;

[Command($"{Convert} {Invoice}", Description = "Convert invoices from xml to json and back")]
public class InvoiceCommand : ConvertCommand, ICommand {

    public const string Invoice = "invoice";

    public async ValueTask ExecuteAsync(IConsole console) {

        if (!File.Exists(From)) {
            throw new InvalidOperationException($"Source {From} does not exist.");
        }

        var srcExt = Path.GetExtension(From);
        var dstExt = Path.GetExtension(To);

        InvoiceType? invoice;

        switch (srcExt, dstExt) {
            case (Consts.Extensions.XML, Consts.Extensions.JSON):
                invoice = await FromXML(From!);
                var json = await ToJSON(invoice);
                await File.WriteAllTextAsync(To!, json).ConfigureAwait(false);
                break;
            case (Consts.Extensions.JSON, Consts.Extensions.XML):
                invoice = await FromJSON(From!);
                var xml = await ToXML(invoice);
                await File.WriteAllTextAsync(To!, xml).ConfigureAwait(false);
                break;
            default:
                console.Output.WithForegroundColor(ConsoleColor.Red,
                                                   async writer => await writer.WriteLineAsync($"Unable to convert from: {From!} to: {To!}"));
                break;
        }
    }

    protected async Task<InvoiceType> FromXML(string file) {
        var xml = await File.ReadAllTextAsync(file).ConfigureAwait(false);
        var invoice = UblDocument.Parse<InvoiceType>(xml);
        return invoice;
    }

    protected Task<string> ToXML(InvoiceType invoice) {
        var tw = new StringWriter();
        UblDocument.Save(invoice, tw);
        return Task.FromResult(tw.ToString());
    }

    protected Task<string> ToJSON(InvoiceType invoice) {
        var doc = UblDocument.ToXDocument(invoice);
        doc!.Document!.Declaration!.Encoding = "utf-8"; 
        var json = JsonConvert.SerializeXNode(doc, Formatting.Indented, false);
        return Task.FromResult(json);
    }

    protected async Task<InvoiceType> FromJSON(string file) {
        var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
        var doc = JsonConvert.DeserializeXNode(json);
        doc!.Document!.Declaration!.Encoding = "utf-8"; 
        var invoice = UblDocument.Parse<InvoiceType>(doc!.ToString());
        invoice.Xmlns = new XmlSerializerNamespaces(new[]
        {
            new XmlQualifiedName("cac","urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"),
            new XmlQualifiedName("cbc","urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"),
            new XmlQualifiedName("cec","urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"),
            new XmlQualifiedName("xsi","http://www.w3.org/2001/XMLSchema-instance"),
            new XmlQualifiedName("xsd","http://www.w3.org/2001/XMLSchema"),
            new XmlQualifiedName("sbt","http://mfin.gov.rs/srbdt/srbdtext"),
            // <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">
            
            
        });        
        return invoice;
    }


}