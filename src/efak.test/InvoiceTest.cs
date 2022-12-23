using UblSharp;

namespace efak.test;

public class InvoiceTest {
    [Fact]
    public void Load() {

        using var fs = File.OpenRead("examples/invoice/full-invoice.xml");
        var invoice = UblDocument.Load<InvoiceType>(fs);

        Assert.NotNull(invoice);
        Assert.Equal("TOSL108", invoice.ID.Value);
        Assert.Equal("123", invoice.OrderReference.ID.Value);
        Assert.Single(invoice.ContractDocumentReference);
        Assert.Collection(invoice.ContractDocumentReference, x => Assert.Equal("Contract321", x.ID.Value));

    }
}