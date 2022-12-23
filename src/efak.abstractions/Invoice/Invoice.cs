namespace efak.abstractions.Invoice;

public class Invoice {

    public string    Number                 { get; set; }
    public DateTime  IssueDate              { get; set; }
    public string    TypeCode               { get; set; }
    public string    CurrencyCode           { get; set; }
    public string?   AccountingCurrencyCode { get; set; }
    public string    VATPointDateCode       { get; set; }
    public DateTime? PaymentDueDate         { get; set; }
    public string?   ByuerReference         { get; set; }
    public string?   ProjectReference       { get; set; }
    public string?   ContractReference      { get; set; }
    public string?   PurchaseOrder          { get; set; }
    public string?   SalesOrderReference    { get; set; }
}