using Typin;
using Typin.Attributes;
using Typin.Console;
using Typin.Utilities;

namespace efak.cli.Sales;

[Command($"{Sales} {ExemptionReasons}")]
public class ExemptionReasonsList : SalesCommand, ICommand {

    public const string ExemptionReasons = "exemptionreasons";

    public async ValueTask ExecuteAsync(IConsole console) {

        var cl = CreateClient();
        var res = await cl.ApiPublicApiSalesInvoiceGetValueAddedTaxExemptionReasonListAsync(ApiKey);

        TableUtils.Write(console.Output,
                         res.Result,
                         new string[] { "Article", "Category", "Point", "Key", "Law", "Paragraph" },
                         footnotes: "",
                         x => x.Article ?? "",
                         x => x.Category ?? "",
                         x => x.Point ?? "",
                         x => x.Key ?? "",
                         x => x.Law ?? "",
                         x => x.Paragraph ?? "",
                         x => x.Subpoint ?? "",
                         x => x.Text ?? "",
                         x => x.FreeFormNote ?? "",
                         x => x.ActiveFrom.ToString(),
                         x => x.ActiveTo.HasValue ? x.ActiveTo.Value.ToString() : ""
        );


    }
}