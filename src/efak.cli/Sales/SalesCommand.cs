using efak.client;

using Typin.Attributes;

namespace efak.cli.Sales;

[Command(SalesCommand.Sales)]
public class SalesCommand : CommandBase {

    public const string Sales = "sales";
    
    [CommandOption("api-key", 'k', Description = "Api key", IsRequired = true)]
    public string? ApiKey { get; set; }

    [CommandOption("service-uri", 's', Description = "Service uri")]
    public string Uri { get; set; } = "https://efaktura.mfin.gov.rs";


    protected virtual IClient CreateClient() {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(Uri);
        //httpClient.DefaultRequestHeaders.Add("ApiKey", ApiKey);
        var res = new Client(httpClient) {
            JsonSerializerSettings = {
                PropertyNameCaseInsensitive = true
            }
        };
        return res;
    }
    
}