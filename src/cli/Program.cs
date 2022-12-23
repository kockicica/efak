using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

using Newtonsoft.Json;

using UblSharp;

using JsonSerializer = Newtonsoft.Json.JsonSerializer;



// var invoiceFiles = Directory.EnumerateFiles("examples/invoice", "*.xml");
// foreach (var invoiceFile in invoiceFiles) {
//     using var fs = File.OpenRead(invoiceFile);
//     var invoice = UblDocument.Load<InvoiceType>(fs);
//     if (invoice.AdditionalDocumentReference.Any()) {
//         Console.WriteLine("Has attachments");
//         foreach (var referenceType in invoice.AdditionalDocumentReference) {
//             if (referenceType.Attachment is {EmbeddedDocumentBinaryObject: {mimeCode: "application/pdf"}}) {
//                 var content = referenceType.Attachment.EmbeddedDocumentBinaryObject.Value;
//                 File.WriteAllBytes("attachment.pdf", content);
//                 Console.WriteLine("Has PDF");
//             }
//         }
//     }
//     var tc = invoice.InvoiceTypeCode;
//     var id = invoice.ID;
//     Console.WriteLine($"Invoice ID: {invoice.ID.Value}");
//
// }


using var fs = File.OpenRead("examples/invoice/full-invoice.xml");
var invoice = UblDocument.Load<InvoiceType>(fs);
var xdoc = UblDocument.ToXDocument(invoice);
//
// // Console.WriteLine(JsonSerializer.Serialize(invoice,
// //                                            new JsonSerializerOptions(JsonSerializerDefaults.Web)
// //                                                { WriteIndented = true, ReferenceHandler = ReferenceHandler.Preserve, MaxDepth = 1024,  }));
//
// var ss = new JsonSerializerSettings {
//     ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
//     PreserveReferencesHandling = PreserveReferencesHandling.All,
//     Formatting = Formatting.Indented,
//     MissingMemberHandling = MissingMemberHandling.Ignore
// };
var json = JsonConvert.SerializeXNode(xdoc, Formatting.Indented, false);
File.WriteAllText("full-invoice.json", json);
//Console.WriteLine(json);

var newXdoc = JsonConvert.DeserializeXNode(json);
var newInvoice = UblDocument.Parse<InvoiceType>(newXdoc.ToString());
UblDocument.Save(newInvoice, "full-invoice-again.xml");