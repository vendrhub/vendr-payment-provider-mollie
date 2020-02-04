using Newtonsoft.Json;
using System.Collections.Generic;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public interface IMollieHalResource
    {
        [JsonProperty("_links")]
        IDictionary<string, MollieHalLink> Links { get; set; }
    }
}
