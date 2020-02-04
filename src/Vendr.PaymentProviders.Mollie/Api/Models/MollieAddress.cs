using Newtonsoft.Json;

namespace Vendr.PaymentProviders.Mollie.Api.Models
{
    public class MollieAddress
    {
        [JsonProperty("organizationName")]
        public string OrganizationName { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("givenName")]
        public string GivenName { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("streetAndNumber")]
        public string StreetAndNumber { get; set; }

        [JsonProperty("streetAdditional")]
        public string StreetAdditional { get; set; }

        [JsonProperty("postalCode")]
        public string PostalCode { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("region")]
        public string Region { get; set; }

        // ISO 3166-1 alpha-2
        [JsonProperty("country")]
        public string Country { get; set; }

    }
}
